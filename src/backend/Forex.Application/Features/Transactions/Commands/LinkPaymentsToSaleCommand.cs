namespace Forex.Application.Features.Transactions.Commands;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Interfaces;
using Forex.Domain.Entities.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Savdoga biriktirilgan to'lovlar to'plamini TransactionIds ga moslaydi:
// ro'yxatdagilarni biriktiradi, ilgari biriktirilgan, lekin ro'yxatda yo'qlarni uzadi.
public sealed record LinkPaymentsToSaleCommand(
    long SaleId,
    List<long> TransactionIds,
    DateTime? DueDate)
    : IRequest<bool>;

public sealed class LinkPaymentsToSaleCommandHandler(IAppDbContext context)
    : IRequestHandler<LinkPaymentsToSaleCommand, bool>
{
    public async Task<bool> Handle(LinkPaymentsToSaleCommand request, CancellationToken cancellationToken)
    {
        var sale = await context.Sales
            .FirstOrDefaultAsync(s => s.Id == request.SaleId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(Sale), nameof(request.SaleId), request.SaleId);

        if (request.DueDate is { } due && due.Date <= sale.Date.Date)
            throw new ConflictException("Qarzni to'lash sanasi to'lov sanasidan kamida bir kun keyin bo'lishi kerak.");

        var keepIds = request.TransactionIds ?? [];

        // 1. Ilgari shu savdoga biriktirilgan to'lovlardan ro'yxatda yo'qlarini uzamiz.
        var currentlyLinked = await context.Transactions
            .Where(t => t.SaleId == sale.Id && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var payment in currentlyLinked)
            if (!keepIds.Contains(payment.Id))
                payment.SaleId = null;

        // 2. Ro'yxatdagi to'lovlarni shu savdoga biriktiramiz (yangidan yoki qaytadan).
        if (keepIds.Count > 0)
        {
            var toLink = await context.Transactions
                .Where(t => keepIds.Contains(t.Id)
                    && t.UserId == sale.CustomerId
                    && (t.SaleId == null || t.SaleId == sale.Id)
                    && !t.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var payment in toLink)
                payment.SaleId = sale.Id;
        }

        if (request.DueDate is { } dueDate)
        {
            var customer = await context.Users
                .FirstOrDefaultAsync(u => u.Id == sale.CustomerId, cancellationToken);

            if (customer is not null)
            {
                var account = await context.UserAccounts
                    .FirstOrDefaultAsync(a => a.UserId == sale.CustomerId
                        && a.CurrencyId == customer.SettlementCurrencyId, cancellationToken)
                    ?? await context.UserAccounts
                        .FirstOrDefaultAsync(a => a.UserId == sale.CustomerId, cancellationToken);

                if (account is not null)
                    account.DueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc);
            }
        }

        return await context.SaveAsync(cancellationToken);
    }
}
