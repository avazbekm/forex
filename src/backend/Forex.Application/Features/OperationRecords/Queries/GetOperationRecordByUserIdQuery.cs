namespace Forex.Application.Features.OperationRecords.Queries;

using AutoMapper;
using Forex.Application.Common.Extensions;
using Forex.Application.Common.Interfaces;
using Forex.Application.Features.OperationRecords.DTOs;
using Forex.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;


public record GetOperationRecordByUserIdQuery(
    long UserId,
    DateTime Begin,
    DateTime End) : IRequest<OperationRecordTurnoverDto>;

public class GetOperationRecordByUserIdQueryHandler(IAppDbContext _context, IMapper mapper)
    : IRequestHandler<GetOperationRecordByUserIdQuery, OperationRecordTurnoverDto>
{

    public async Task<OperationRecordTurnoverDto> Handle(
        GetOperationRecordByUserIdQuery request,
        CancellationToken ct)
    {
        var account = await GetSettlementAccountAsync(request.UserId, ct);
        var openingBalance = account?.OpeningBalance ?? 0;

        var allRecords = await GetAllUserOperationRecordsAsync(request.UserId, ct);

        var beginBalance = CalculateBalance(openingBalance, allRecords, request.Begin, isEndDate: false);
        var endBalance = CalculateBalance(openingBalance, allRecords, request.End, isEndDate: true);

        var operationsInRange = mapper.Map<List<OperationRecordDto>>(
        FilterOperationRecordsInRange(allRecords, request.Begin, request.End)
        );

        return new OperationRecordTurnoverDto
        {
            BeginBalance = beginBalance,
            EndBalance = endBalance,
            SettlementCurrencyId = account?.CurrencyId ?? 0,
            SettlementCurrencyCode = account?.Currency?.Code,
            OperationRecords = operationsInRange
        };
    }

    private async Task<UserAccount?> GetSettlementAccountAsync(long userId, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return null;

        return await _context.UserAccounts
            .Include(a => a.Currency)
            .FirstOrDefaultAsync(a => a.UserId == userId && a.CurrencyId == user.SettlementCurrencyId, ct);
    }

    private async Task<List<OperationRecord>> GetAllUserOperationRecordsAsync(long userId, CancellationToken ct)
    {
        return await _context.OperationRecords
            .Include(x => x.Currency)
            .Include(x => x.Sale)
            .Include(x => x.Transaction)
            .Include(x => x.Supply)
            .Where(or =>
                or.UserId == userId ||
                (or.Sale != null && or.Sale.CustomerId == userId) ||
                (or.Transaction != null && or.Transaction.UserId == userId) ||
                (or.Supply != null && or.Supply.UserId == userId)
            )
            .ToListAsync(ct);
    }

    private static decimal CalculateBalance(decimal openingBalance, List<OperationRecord> all, DateTime date, bool isEndDate)
    {
        var turnover = all
            .Where(or => isEndDate ? or.Date < date.AddDays(1).ToUtcSafe() : or.Date < date.ToUtcSafe())
            .Sum(or => or.Amount * or.Rate);

        return openingBalance + turnover;
    }

    private static List<OperationRecord> FilterOperationRecordsInRange(
        List<OperationRecord> all,
        DateTime begin,
        DateTime end) => [.. all
            .Where(or => or.Date >= begin.ToUtcSafe() && or.Date < end.AddDays(1).ToUtcSafe())
            .OrderBy(or => or.Date)];
}
