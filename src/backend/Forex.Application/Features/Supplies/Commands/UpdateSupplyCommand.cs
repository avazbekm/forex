namespace Forex.Application.Features.Supplies.Commands;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Extensions;
using Forex.Application.Common.Interfaces;
using Forex.Domain.Entities;
using Forex.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record UpdateSupplyCommand(
    long Id,
    DateTime Date,
    SupplyPartyType PartyType,
    long UserId,
    decimal Amount,
    long CurrencyId,
    string? Description) : IRequest<bool>;

public sealed class UpdateSupplyCommandHandler(IAppDbContext context)
    : IRequestHandler<UpdateSupplyCommand, bool>
{
    public async Task<bool> Handle(UpdateSupplyCommand request, CancellationToken ct)
    {
        Validate(request);

        await context.BeginTransactionAsync(ct);

        try
        {
            var supply = await context.Supplies.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Supply), nameof(request.Id), request.Id);
            var operation = await context.OperationRecords.FirstOrDefaultAsync(or => or.SupplyId == supply.Id, ct);

            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
                ?? throw new NotFoundException(nameof(User), nameof(request.UserId), request.UserId);

            ValidateUserRole(request.PartyType, user);

            var currency = await context.Currencies.FirstOrDefaultAsync(c => c.Id == request.CurrencyId, ct)
                ?? throw new NotFoundException(nameof(Currency), nameof(request.CurrencyId), request.CurrencyId);

            if (operation is not null)
            {
                var oldAccount = await context.GetSettlementAccountAsync(supply.UserId, ct);
                oldAccount.Balance -= operation.Amount * operation.Rate;
            }

            supply.Date = ToUtc(request.Date);
            supply.PartyType = request.PartyType;
            supply.UserId = user.Id;
            supply.Amount = request.Amount;
            supply.CurrencyId = currency.Id;
            supply.Description = request.Description;

            var (rate, _) = await context.ApplyToSettlementAsync(
                supply.UserId, supply.CurrencyId, currency.ExchangeRate, supply.Amount, ct);

            if (operation is not null)
            {
                operation.Date = supply.Date;
                operation.Amount = supply.Amount;
                operation.Rate = rate;
                operation.CurrencyId = supply.CurrencyId;
                operation.Description = BuildDescription(supply, user, currency);
                operation.Type = OperationType.Supply;
                operation.UserId = supply.UserId;
            }
            else
            {
                context.OperationRecords.Add(new OperationRecord
                {
                    Type = OperationType.Supply,
                    Date = supply.Date,
                    Amount = supply.Amount,
                    Rate = rate,
                    CurrencyId = supply.CurrencyId,
                    Description = BuildDescription(supply, user, currency),
                    UserId = supply.UserId,
                    Supply = supply
                });
            }

            await context.CommitTransactionAsync(ct);
            return true;
        }
        catch
        {
            await context.RollbackTransactionAsync(ct);
            throw;
        }
    }

    private static void Validate(UpdateSupplyCommand request)
    {
        if (request.Id <= 0)
            throw new AppException("Ta'minot yozuvi topilmadi.");

        if (request.UserId <= 0)
            throw new AppException("Ta'minotchi yoki vositachi tanlanishi shart.");

        if (request.CurrencyId <= 0)
            throw new AppException("Valyuta tanlanishi shart.");

        if (request.Amount <= 0)
            throw new AppException("Summa 0 dan katta bo'lishi shart.");
    }

    private static void ValidateUserRole(SupplyPartyType partyType, User user)
    {
        if (partyType == SupplyPartyType.Supplier && user.Role != UserRole.Taminotchi)
            throw new AppException("Tanlangan user ta'minotchi emas.");

        if (partyType == SupplyPartyType.Consolidator && user.Role != UserRole.Vositachi)
            throw new AppException("Tanlangan user vositachi emas.");
    }

    private static DateTime ToUtc(DateTime date) => date.Kind switch
    {
        DateTimeKind.Utc => date,
        DateTimeKind.Local => date.ToUniversalTime(),
        _ => DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime()
    };

    private static string BuildDescription(Supply supply, User user, Currency currency)
    {
        var party = supply.PartyType == SupplyPartyType.Supplier ? "Ta'minotchi" : "Vositachi";
        return $"{party}: {user.Name}, {supply.Amount:N2} {currency.Code}\n{supply.Description}".Trim();
    }
}
