namespace Forex.Application.Features.Transactions.Commands;

using AutoMapper;
using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Extensions;
using Forex.Application.Common.Interfaces;
using Forex.Domain.Entities;
using Forex.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

public sealed record CreateTransactionCommand(
    decimal Amount,
    decimal ExchangeRate,
    decimal Discount,
    PaymentMethod PaymentMethod,
    bool IsIncome,
    string? Description,
    DateTime Date,
    DateTime DueDate,
    long UserId,
    long CurrencyId,
    long? SaleId = null)
    : IRequest<long>;

public class CreateTransactionCommandHandler(
    IAppDbContext context,
    IMapper mapper) : IRequestHandler<CreateTransactionCommand, long>
{
    public async Task<long> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var transaction = await CreateTransactionAsync(request, cancellationToken);

            await context.CommitTransactionAsync(cancellationToken);
            return transaction.Id;
        }
        catch
        {
            await context.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Transaction> CreateTransactionAsync(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        var shop = await context.Shops
            .FirstOrDefaultAsync(cancellationToken);

        if (shop is null)
        {
            shop = new Shop
            {
                Name = "Default",
                NormalizedName = "DEFAULT"
            };

            context.Shops.Add(shop);
        }

        var transaction = mapper.Map<Transaction>(request);
        transaction.Shop = shop;

        var description = await GenerateDescription(transaction);

        var discountInOp = transaction.IsIncome && transaction.Discount > 0 && transaction.ExchangeRate != 0
            ? transaction.Discount / transaction.ExchangeRate
            : 0m;
        var signedAmount = transaction.IsIncome
            ? transaction.Amount + discountInOp
            : -transaction.Amount;

        var (rate, account) = await context.ApplyToSettlementAsync(
            transaction.UserId, transaction.CurrencyId, transaction.ExchangeRate, signedAmount, cancellationToken);

        account.DueDate = DateTime.SpecifyKind(request.DueDate, DateTimeKind.Utc);

        transaction.OperationRecord = new()
        {
            Amount = signedAmount,
            Rate = rate,
            CurrencyId = transaction.CurrencyId,
            Date = transaction.Date,
            Description = description,
            Type = OperationType.Transaction,
            UserId = transaction.UserId
        };

        context.Transactions.Add(transaction);
        return transaction;
    }

    private async Task<string> GenerateDescription(Transaction transaction)
    {
        var currency = await context.Currencies.FirstOrDefaultAsync(c => c.Id == transaction.CurrencyId)
            ?? throw new NotFoundException(nameof(Currency), nameof(transaction.CurrencyId), transaction.CurrencyId);

        if (transaction.Discount < 0)
            throw new ForbiddenException("Chegirma 0 dan kichik bo'lishi mumkin emas!");

        var isWithDiscount = transaction.Discount > 0;

        return (transaction.PaymentMethod switch
        {
            PaymentMethod.Naqd => $"Naqd to'lov: {transaction.Amount} {currency.Code}, Kurs: {transaction.ExchangeRate} UZS{(isWithDiscount ? $", Chegirma: {transaction.Discount} UZS" : string.Empty)}",
            PaymentMethod.Plastik => $"Karta to'lov: {transaction.Amount} {currency.Code}, Kurs: {transaction.ExchangeRate} UZS{(isWithDiscount ? $", Chegirma: {transaction.Discount} UZS" : string.Empty)}",
            PaymentMethod.HisobRaqam => $"Hisob raqam orqali to'lov: {transaction.Amount} {currency.Code}, Kurs: {transaction.ExchangeRate} UZS{(isWithDiscount ? $", Chegirma: {transaction.Discount} UZS" : string.Empty)}",
            PaymentMethod.MobilIlova => $"Online to'lov: {transaction.Amount} {currency.Code}, Kurs: {transaction.ExchangeRate} UZS{(isWithDiscount ? $", Chegirma: {transaction.Discount} UZS" : string.Empty)}",
            _ => "Noma'lum to'lov usuli",
        }) + "\n"
        + transaction.Description;
    }
}
