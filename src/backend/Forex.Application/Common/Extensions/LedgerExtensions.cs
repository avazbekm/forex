namespace Forex.Application.Common.Extensions;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Interfaces;
using Forex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public static class LedgerExtensions
{
    public static async Task<Currency> GetBaseCurrencyAsync(this IAppDbContext context, CancellationToken ct)
        => await context.Currencies.FirstOrDefaultAsync(c => c.IsDefault, ct)
            ?? await context.Currencies.FirstOrDefaultAsync(c => c.Code == "UZS", ct)
            ?? throw new NotFoundException(nameof(Currency), nameof(Currency.IsDefault), true);

    public static async Task<UserAccount> GetSettlementAccountAsync(
        this IAppDbContext context, long userId, CancellationToken ct)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException(nameof(User), nameof(userId), userId);

        return await context.UserAccounts
            .FirstOrDefaultAsync(a => a.UserId == userId && a.CurrencyId == user.SettlementCurrencyId, ct)
            ?? throw new NotFoundException(nameof(UserAccount), nameof(userId), userId);
    }

    public static async Task<(decimal Rate, UserAccount Account)> ApplyToSettlementAsync(
        this IAppDbContext context,
        long userId,
        long operationCurrencyId,
        decimal operationRateToBase,
        decimal signedAmount,
        CancellationToken ct)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException(nameof(User), nameof(userId), userId);

        var rate = await context.SettlementRateAsync(operationCurrencyId, operationRateToBase, user.SettlementCurrencyId, ct);

        var account = await context.UserAccounts
            .FirstOrDefaultAsync(a => a.UserId == userId && a.CurrencyId == user.SettlementCurrencyId, ct);

        if (account is null)
        {
            account = new UserAccount { UserId = userId, CurrencyId = user.SettlementCurrencyId };
            context.UserAccounts.Add(account);
        }

        account.Balance += signedAmount * rate;
        return (rate, account);
    }

    public static async Task<decimal> SettlementRateAsync(
        this IAppDbContext context,
        long operationCurrencyId,
        decimal operationRateToBase,
        long settlementCurrencyId,
        CancellationToken ct)
    {
        if (operationCurrencyId == settlementCurrencyId)
            return 1m;

        var settlement = await context.Currencies.FirstOrDefaultAsync(c => c.Id == settlementCurrencyId, ct)
            ?? throw new NotFoundException(nameof(Currency), nameof(settlementCurrencyId), settlementCurrencyId);

        var settlementRateToBase = settlement.IsDefault || settlement.ExchangeRate == 0 ? 1m : settlement.ExchangeRate;
        return operationRateToBase / settlementRateToBase;
    }
}
