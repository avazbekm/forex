namespace Forex.Application.Features.Transactions.Commands;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Extensions;
using Forex.Application.Common.Interfaces;
using Forex.Domain.Entities;
using Forex.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record UpdateTransactionCommand(
    long Id,
    decimal Amount,
    decimal ExchangeRate,
    decimal Discount,
    PaymentMethod PaymentMethod,
    bool IsIncome,
    string? Description,
    DateTime Date,
    DateTime DueDate,
    long UserId,
    long CurrencyId)
    : IRequest<bool>;

public class UpdateTransactionCommandHandler(
    IAppDbContext context) : IRequestHandler<UpdateTransactionCommand, bool>
{
    public async Task<bool> Handle(UpdateTransactionCommand request, CancellationToken cancellationToken)
    {
        await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var existingTransaction = await context.Transactions
                .Include(t => t.OperationRecord)
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(Transaction), nameof(request.Id), request.Id);

            await RevertAsync(existingTransaction, cancellationToken);
            await UpdateTransactionAsync(existingTransaction, request, cancellationToken);

            await context.CommitTransactionAsync(cancellationToken);
            return true;
        }
        catch
        {
            await context.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task UpdateTransactionAsync(
        Transaction existingTransaction,
        UpdateTransactionCommand request,
        CancellationToken cancellationToken)
    {
        existingTransaction.Amount = request.Amount;
        existingTransaction.ExchangeRate = request.ExchangeRate;
        existingTransaction.Discount = request.Discount;
        existingTransaction.PaymentMethod = request.PaymentMethod;
        existingTransaction.IsIncome = request.IsIncome;
        existingTransaction.Description = request.Description;
        existingTransaction.Date = request.Date.ToUtcSafe();
        existingTransaction.UserId = request.UserId;
        existingTransaction.CurrencyId = request.CurrencyId;

        var description = await GenerateDescription(existingTransaction);

        var discountInOp = existingTransaction.IsIncome && existingTransaction.Discount > 0 && existingTransaction.ExchangeRate != 0
            ? existingTransaction.Discount / existingTransaction.ExchangeRate
            : 0m;
        var signedAmount = existingTransaction.IsIncome
            ? existingTransaction.Amount + discountInOp
            : -existingTransaction.Amount;

        var (rate, account) = await context.ApplyToSettlementAsync(
            existingTransaction.UserId, existingTransaction.CurrencyId, existingTransaction.ExchangeRate, signedAmount, cancellationToken);
        account.DueDate = DateTime.SpecifyKind(request.DueDate, DateTimeKind.Utc);

        var record = existingTransaction.OperationRecord ?? new OperationRecord();
        record.Amount = signedAmount;
        record.Rate = rate;
        record.CurrencyId = existingTransaction.CurrencyId;
        record.Date = existingTransaction.Date.ToUtcSafe();
        record.Description = description;
        record.Type = OperationType.Transaction;
        record.UserId = existingTransaction.UserId;
        existingTransaction.OperationRecord = record;
    }

    private async Task RevertAsync(Transaction transaction, CancellationToken ct)
    {
        if (transaction.OperationRecord is null)
            return;

        var account = await context.GetSettlementAccountAsync(transaction.UserId, ct);
        account.Balance -= transaction.OperationRecord.Amount * transaction.OperationRecord.Rate;
    }

    private async Task<string> GenerateDescription(Transaction transaction)
    {
        var currency = await context.Currencies.FirstOrDefaultAsync(c => c.Id == transaction.CurrencyId)
            ?? throw new NotFoundException(nameof(Currency), nameof(transaction.CurrencyId), transaction.CurrencyId);

        if (transaction.Discount < 0)
            throw new ForbiddenException("Chegirma 0 dan kichik bo'lishi mumkin emas!");

        var isWithDiscount = transaction.Discount > 0;

        return transaction.PaymentMethod switch
        {
            PaymentMethod.Naqd => $"Naqd to'lov: {transaction.Amount} {currency.Code}, Kurs: {transaction.ExchangeRate} UZS{(isWithDiscount ? $", Chegirma: {transaction.Discount} UZS" : string.Empty)}",
            PaymentMethod.Plastik => $"Karta to'lov: {transaction.Amount} {currency.Code}, Kurs: {transaction.ExchangeRate} UZS{(isWithDiscount ? $", Chegirma: {transaction.Discount} UZS" : string.Empty)}",
            PaymentMethod.HisobRaqam => $"Hisob raqam orqali to'lov: {transaction.Amount} {currency.Code}, Kurs: {transaction.ExchangeRate} UZS{(isWithDiscount ? $", Chegirma: {transaction.Discount} UZS" : string.Empty)}",
            PaymentMethod.MobilIlova => $"Online to'lov: {transaction.Amount} {currency.Code}, Kurs: {transaction.ExchangeRate} UZS{(isWithDiscount ? $", Chegirma: {transaction.Discount} UZS" : string.Empty)}",
            _ => "Noma'lum to'lov usuli",
        };
    }
}
