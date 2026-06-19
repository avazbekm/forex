namespace Forex.Application.Features.Transactions.Commands;

using AutoMapper;
using Forex.Application.Common.Exceptions;
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
    long CurrencyId)
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
            await UpdateUserAccountAsync(request, transaction, cancellationToken);
            await UpdateCurrencyExchangeRate(transaction.CurrencyId, transaction.ExchangeRate);

            await context.CommitTransactionAsync(cancellationToken);
            return transaction.Id;
        }
        catch
        {
            await context.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task UpdateCurrencyExchangeRate(long currencyId, decimal exchangeRate)
    {
        var currency = await context.Currencies.FirstOrDefaultAsync(c => c.Id == currencyId)
            ?? throw new NotFoundException(nameof(Currency), nameof(currencyId), currencyId);

        currency.ExchangeRate = exchangeRate;
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
        var amount = transaction.Amount * transaction.ExchangeRate + (transaction.IsIncome ? transaction.Discount : 0);

        transaction.OperationRecord = new()
        {
            Amount = transaction.IsIncome ? amount : -amount,
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

    private async Task UpdateUserAccountAsync(CreateTransactionCommand request, Transaction transaction, CancellationToken cancellationToken)
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
