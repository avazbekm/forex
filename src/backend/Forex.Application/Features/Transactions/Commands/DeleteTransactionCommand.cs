namespace Forex.Application.Features.Transactions.Commands;

using Forex.Application.Common.Exceptions;
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

            await RevertUserAccountAsync(transaction, ct);

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

    private async Task RevertUserAccountAsync(Transaction transaction, CancellationToken ct)
    {
        var uzsCurrency = await context.Currencies
            .FirstOrDefaultAsync(c => c.Code == "UZS", ct)
            ?? throw new InvalidOperationException("UZS currency not found");

        var userAccount = await context.UserAccounts
            .FirstOrDefaultAsync(a => a.UserId == transaction.UserId && a.CurrencyId == uzsCurrency.Id, ct)
            ?? throw new NotFoundException(nameof(UserAccount), nameof(transaction.UserId), transaction.UserId);

        var amountInUZS = transaction.Amount * transaction.ExchangeRate;
        var delta = amountInUZS + transaction.Discount;

        if (transaction.IsIncome)
            userAccount.Balance -= delta;
        else
            userAccount.Balance += delta;
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
