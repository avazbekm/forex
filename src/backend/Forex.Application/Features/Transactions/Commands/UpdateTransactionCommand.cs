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

            await RevertUserAccountAsync(existingTransaction, cancellationToken);

            await UpdateTransactionAsync(existingTransaction, request, cancellationToken);
            await UpdateUserAccountAsync(request, existingTransaction, cancellationToken);
            await UpdateCurrencyExchangeRate(request.CurrencyId, request.ExchangeRate);

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
        var amount = existingTransaction.Amount * existingTransaction.ExchangeRate +
                    (existingTransaction.IsIncome ? existingTransaction.Discount : 0);
        var operationAmount = existingTransaction.IsIncome ? amount : -amount;

        if (existingTransaction.OperationRecord is not null)
        {
            existingTransaction.OperationRecord.Amount = operationAmount;
            existingTransaction.OperationRecord.Date = existingTransaction.Date.ToUtcSafe();
            existingTransaction.OperationRecord.Description = description;
            existingTransaction.OperationRecord.Type = OperationType.Transaction;
            existingTransaction.OperationRecord.UserId = existingTransaction.UserId;
        }
        else
        {
            existingTransaction.OperationRecord = new OperationRecord
            {
                Amount = operationAmount,
                Date = existingTransaction.Date.ToUtcSafe(),
                Description = description,
                Type = OperationType.Transaction,
                UserId = existingTransaction.UserId
            };
        }
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

    private async Task UpdateCurrencyExchangeRate(long currencyId, decimal exchangeRate)
    {
        var currency = await context.Currencies.FirstOrDefaultAsync(c => c.Id == currencyId)
            ?? throw new NotFoundException(nameof(Currency), nameof(currencyId), currencyId);

        currency.ExchangeRate = exchangeRate;
    }

    private async Task RevertUserAccountAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        var uzsCurrency = await context.Currencies
            .FirstOrDefaultAsync(c => c.Code == "UZS", cancellationToken)
            ?? throw new NotFoundException(nameof(Currency), nameof(Currency.Code), "UZS");

        var userAccount = await context.UserAccounts
            .FirstOrDefaultAsync(a => a.UserId == transaction.UserId && a.CurrencyId == uzsCurrency.Id, cancellationToken)
            ?? throw new NotFoundException("Customer account not found");

        var amountInUZS = transaction.Amount * transaction.ExchangeRate;
        var delta = amountInUZS + transaction.Discount;
        if (transaction.IsIncome)
            userAccount.Balance -= delta;
        else
            userAccount.Balance += delta;
    }

    private async Task UpdateUserAccountAsync(
        UpdateTransactionCommand request,
        Transaction transaction,
        CancellationToken cancellationToken)
    {
        var uzsCurrency = await context.Currencies
            .FirstOrDefaultAsync(c => c.Code == "UZS", cancellationToken)
            ?? throw new NotFoundException(nameof(Currency), nameof(Currency.Code), "UZS");

        var userAccount = await context.UserAccounts
            .FirstOrDefaultAsync(a => a.UserId == request.UserId && a.CurrencyId == uzsCurrency.Id, cancellationToken);

        if (userAccount is null)
        {
            userAccount = new UserAccount
            {
                UserId = request.UserId,
                CurrencyId = uzsCurrency.Id,
                OpeningBalance = 0,
                Balance = 0,
                Discount = 0
            };
            context.UserAccounts.Add(userAccount);
        }

        var amountInUZS = request.Amount * request.ExchangeRate;
        var delta = amountInUZS + request.Discount;

        userAccount.DueDate = DateTime.SpecifyKind(request.DueDate, DateTimeKind.Utc);
        if (request.IsIncome)
            userAccount.Balance += delta;
        else
            userAccount.Balance -= delta;
    }
}
