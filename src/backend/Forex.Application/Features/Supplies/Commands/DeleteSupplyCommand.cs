namespace Forex.Application.Features.Supplies.Commands;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Interfaces;
using Forex.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record DeleteSupplyCommand(long Id) : IRequest<bool>;

public sealed class DeleteSupplyCommandHandler(IAppDbContext context)
    : IRequestHandler<DeleteSupplyCommand, bool>
{
    public async Task<bool> Handle(DeleteSupplyCommand request, CancellationToken ct)
    {
        await context.BeginTransactionAsync(ct);

        try
        {
            var supply = await context.Supplies.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
                ?? throw new NotFoundException(nameof(Supply), nameof(request.Id), request.Id);

            var account = await context.UserAccounts
                .FirstOrDefaultAsync(a => a.UserId == supply.UserId && a.CurrencyId == supply.CurrencyId, ct)
                ?? throw new NotFoundException("UserAccount not found");

            var operation = await context.OperationRecords.FirstOrDefaultAsync(or => or.SupplyId == supply.Id, ct);

            account.Balance -= supply.Amount;
            if (operation is not null)
                context.OperationRecords.Remove(operation);
            supply.IsDeleted = true;

            await context.CommitTransactionAsync(ct);
            return true;
        }
        catch
        {
            await context.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
