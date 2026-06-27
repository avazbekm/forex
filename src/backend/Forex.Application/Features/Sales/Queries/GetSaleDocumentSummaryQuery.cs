namespace Forex.Application.Features.Sales.Queries;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Interfaces;
using Forex.Application.Features.Sales.DTOs;
using Forex.Domain.Entities;
using Forex.Domain.Entities.Sales;
using Forex.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

public record GetSaleDocumentSummaryQuery(long Id) : IRequest<SaleDocumentSummaryDto>;

public class GetSaleDocumentSummaryQueryHandler(IAppDbContext context)
    : IRequestHandler<GetSaleDocumentSummaryQuery, SaleDocumentSummaryDto>
{
    public async Task<SaleDocumentSummaryDto> Handle(GetSaleDocumentSummaryQuery request, CancellationToken ct)
    {
        var sale = await context.Sales
            .Include(s => s.OperationRecord)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Sale), nameof(request.Id), request.Id);

        var saleOp = sale.OperationRecord;
        var customerId = sale.CustomerId;

        var customer = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == customerId, ct)
            ?? throw new NotFoundException(nameof(User), nameof(customerId), customerId);

        var settlementCode = await context.Currencies.AsNoTracking()
            .Where(c => c.Id == customer.SettlementCurrencyId)
            .Select(c => c.Code)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        // Joriy hisob balansi — haqiqat manbasi. Mijoz yaratilganda kiritilgan boshlang'ich
        // qoldiq (daftardagi qarz/haq) ham shu balansda, lekin OperationRecord sifatida yo'q.
        var accountBalance = await context.UserAccounts.AsNoTracking()
            .Where(a => a.UserId == customerId && a.CurrencyId == customer.SettlementCurrencyId)
            .Select(a => (decimal?)a.Balance)
            .FirstOrDefaultAsync(ct) ?? 0m;

        // Boshlang'ich qoldiq = joriy balans − barcha operatsiyalar yig'indisi.
        // (Ledger operatsiyalari boshlang'ich qoldiqni o'z ichiga olmaydi.)
        var allOps = await context.OperationRecords.AsNoTracking()
            .Where(o => o.UserId == customerId && !o.IsDeleted)
            .Select(o => new { o.Amount, o.Rate })
            .ToListAsync(ct);
        var openingBalance = accountBalance - allOps.Sum(o => o.Amount * o.Rate);

        // Tarixiy qoldiq — real vaqtda ledgerdan hisoblanadi: boshlang'ich qoldiq + shu savdodan
        // oldingi operatsiyalar (Amount * Rate). Shu savdoga biriktirilgan to'lovlar chiqarib
        // tashlanadi (ular "To'langan"da alohida ko'rsatiladi — ikki marta sanalmasligi uchun).
        var priorRecords = await context.OperationRecords.AsNoTracking()
            .Where(o => o.UserId == customerId && !o.IsDeleted
                && (o.Date < saleOp.Date || (o.Date == saleOp.Date && o.Id < saleOp.Id))
                && !(o.Transaction != null && o.Transaction.SaleId == sale.Id))
            .Select(o => new { o.Amount, o.Rate })
            .ToListAsync(ct);

        var priorBalance = openingBalance + priorRecords.Sum(o => o.Amount * o.Rate);
        var saleAmount = sale.TotalAmount;

        var payments = await context.Transactions.AsNoTracking()
            .Include(t => t.Currency)
            .Include(t => t.OperationRecord)
            .Where(t => t.SaleId == sale.Id && !t.IsDeleted)
            .ToListAsync(ct);

        var groups = payments
            .GroupBy(t => new { t.CurrencyId, t.ExchangeRate })
            .Select(g => new SalePaymentGroupDto
            {
                CurrencyCode = g.First().Currency.Code,
                ExchangeRate = g.Key.ExchangeRate,
                Amount = g.Sum(t => t.Amount),
                SettlementAmount = g.Sum(t => t.OperationRecord.Amount * t.OperationRecord.Rate),
                Methods = string.Join(", ", g.Select(t => MethodText(t.PaymentMethod)).Distinct())
            })
            .OrderBy(g => g.CurrencyCode)
            .ToList();

        var totalPaid = groups.Sum(g => g.SettlementAmount);

        return new SaleDocumentSummaryDto
        {
            SettlementCurrencyCode = settlementCode,
            PriorBalance = priorBalance,
            SaleAmount = saleAmount,
            TotalPaid = totalPaid,
            RemainingBalance = priorBalance - saleAmount + totalPaid,
            Payments = groups
        };
    }

    private static string MethodText(PaymentMethod method) => method switch
    {
        PaymentMethod.Naqd => "Naqd",
        PaymentMethod.Plastik => "Plastik",
        PaymentMethod.HisobRaqam => "Hisob raqam",
        PaymentMethod.MobilIlova => "Mobil ilova",
        _ => "—"
    };
}
