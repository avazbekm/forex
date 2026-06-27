namespace Forex.Application.Features.Users.Commands;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Interfaces;
using Forex.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

public record DeleteUserCommand(long Id) : IRequest<bool>;

public class DeleteUserCommandHandler(IAppDbContext context)
    : IRequestHandler<DeleteUserCommand, bool>
{
    public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException(nameof(User), nameof(request.Id), request.Id);

        var hasOperations =
            await context.Sales.AnyAsync(s => s.CustomerId == user.Id && !s.IsDeleted, cancellationToken)
            || await context.Transactions.AnyAsync(t => t.UserId == user.Id && !t.IsDeleted, cancellationToken)
            || await context.Supplies.AnyAsync(s => s.UserId == user.Id && !s.IsDeleted, cancellationToken)
            || await context.Returns.AnyAsync(r => r.CustomerId == user.Id && !r.IsDeleted, cancellationToken)
            || await context.OperationRecords.AnyAsync(o => o.UserId == user.Id && !o.IsDeleted, cancellationToken);

        if (hasOperations)
            throw new ConflictException(
                "Bu foydalanuvchini o'chirib bo'lmaydi: unda operatsiyalar (savdo, to'lov, ta'minot yoki qaytarish) mavjud. " +
                "Avval o'sha operatsiyalarni o'chiring, so'ng foydalanuvchini o'chirish mumkin bo'ladi.");

        // Operatsiyalar yo'q — foydalanuvchini va unga tegishli qolgan ma'lumotlarni
        // (balans hisoblari va bildirishnomalar) butunlay o'chiramiz (yetim ma'lumot qolmasin).
        var accounts = await context.UserAccounts
            .Where(a => a.UserId == user.Id)
            .ToListAsync(cancellationToken);
        context.UserAccounts.RemoveRange(accounts);

        var notifications = await context.UserNotifications
            .Where(n => n.UserId == user.Id)
            .ToListAsync(cancellationToken);
        context.UserNotifications.RemoveRange(notifications);

        context.Users.Remove(user);

        return await context.SaveAsync(cancellationToken);
    }
}
