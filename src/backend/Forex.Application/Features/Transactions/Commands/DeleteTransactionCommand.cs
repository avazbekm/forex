namespace Forex.Application.Features.Transactions.Commands;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Extensions;
using Forex.Application.Common.Interfaces;
using Forex.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record DeleteTransactionCommand(long TransactionId) : IRequest<bool>;

public class DeleteTransactionCommandHandler(
    IAppDbContext context)
    : IRequestHandler<DeleteTransactionCommand, bool>
{
    public async Task<bool> Handle(DeleteTransactionCommand request, CancellationToken ct)
    {
        await context.BeginTransactionAsync(ct);

        try
        {
            var transaction = await LoadTransactionAsync(request.TransactionId, ct);

            await RevertAsync(transaction, ct);

            RemoveOperationRecord(transaction);
            RemoveTransaction(transaction);

            return await context.CommitTransactionAsync(ct);
        }
        catch
        {
            await context.RollbackTransactionAsync(ct);
            throw;
        }
    }

    private async Task<Transaction> LoadTransactionAsync(long transactionId, CancellationToken ct)
    {
        return await context.Transactions
            .Include(t => t.OperationRecord)
            .FirstOrDefaultAsync(t => t.Id == transactionId, ct)
            ?? throw new NotFoundException(nameof(Transaction), nameof(transactionId), transactionId);
    }

    private async Task RevertAsync(Transaction transaction, CancellationToken ct)
    {
        if (transaction.OperationRecord is null)
            return;

        var account = await context.GetSettlementAccountAsync(transaction.UserId, ct);
        account.Balance -= transaction.OperationRecord.Amount * transaction.OperationRecord.Rate;
    }

    private void RemoveOperationRecord(Transaction transaction)
    {
        if (transaction.OperationRecord is not null)
        {
            context.OperationRecords.Remove(transaction.OperationRecord);
            transaction.OperationRecord = null!;
        }
    }

    private void RemoveTransaction(Transaction transaction)
    {
        context.Transactions.Remove(transaction);
    }
}
