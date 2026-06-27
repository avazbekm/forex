namespace Forex.Application.Features.Transactions.Queries;

using Forex.Application.Common.Interfaces;
using Forex.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Savdoga biriktirish oynasi uchun to'lovlar: berilgan sanadagi BIRIKTIRILMAGAN to'lovlar +
// (tahrirlashda) shu savdoga ALLAQACHON biriktirilgan to'lovlar (sanadan qat'i nazar).
public sealed record GetUnlinkedPaymentsQuery(long UserId, DateTime Date, long? SaleId = null)
    : IRequest<List<UnlinkedPaymentDto>>;

public sealed class UnlinkedPaymentDto
{
    public long Id { get; set; }
    public decimal Amount { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal Discount { get; set; }
    public long CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public bool IsLinkedToSale { get; set; }
}

public sealed class GetUnlinkedPaymentsQueryHandler(IAppDbContext context)
    : IRequestHandler<GetUnlinkedPaymentsQuery, List<UnlinkedPaymentDto>>
{
    public async Task<List<UnlinkedPaymentDto>> Handle(GetUnlinkedPaymentsQuery request, CancellationToken cancellationToken)
    {
        var dayStart = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        var saleId = request.SaleId;

        return await context.Transactions
            .Where(t => !t.IsDeleted
                && t.IsIncome
                && t.UserId == request.UserId
                && ((t.SaleId == null && t.Date >= dayStart && t.Date < dayEnd)
                    || (saleId != null && t.SaleId == saleId)))
            .OrderBy(t => t.Date)
            .Select(t => new UnlinkedPaymentDto
            {
                Id = t.Id,
                Amount = t.Amount,
                ExchangeRate = t.ExchangeRate,
                Discount = t.Discount,
                CurrencyId = t.CurrencyId,
                CurrencyCode = t.Currency.Code,
                PaymentMethod = t.PaymentMethod,
                Description = t.Description,
                Date = t.Date,
                IsLinkedToSale = saleId != null && t.SaleId == saleId
            })
            .ToListAsync(cancellationToken);
    }
}
