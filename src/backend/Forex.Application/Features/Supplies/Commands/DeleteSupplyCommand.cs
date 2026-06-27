namespace Forex.Application.Features.Supplies.Commands;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Extensions;
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

            var operation = await context.OperationRecords.FirstOrDefaultAsync(or => or.SupplyId == supply.Id, ct);

            if (operation is not null)
            {
                var account = await context.GetSettlementAccountAsync(supply.UserId, ct);
                account.Balance -= operation.Amount * operation.Rate;
                context.OperationRecords.Remove(operation);
            }

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
