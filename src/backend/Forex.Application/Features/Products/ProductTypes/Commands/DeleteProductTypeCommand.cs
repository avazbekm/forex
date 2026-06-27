namespace Forex.Application.Features.Products.ProductTypes.Commands;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Interfaces;
using Forex.Domain.Entities.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

public record DeleteProductTypeCommand(long Id) : IRequest<bool>;

public class DeleteProductTypeCommandHandler(IAppDbContext context)
    : IRequestHandler<DeleteProductTypeCommand, bool>
{
    public async Task<bool> Handle(DeleteProductTypeCommand request, CancellationToken ct)
    {
        var productType = await context.ProductTypes
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(ProductType), nameof(request.Id), request.Id);

        // Savdoda qatnashganligini tekshirish
        var hasSales = await context.SaleItems
            .AnyAsync(si => si.ProductTypeId == request.Id, ct);

        if (hasSales)
            throw new ForbiddenException("Bu turdagi mahsulot savdoda qatnashgan. O'chirib bo'lmaydi!");

        await context.BeginTransactionAsync(ct);

        try
        {
            // ProductEntry
            var entries = await context.ProductEntries
                .Where(e => e.ProductTypeId == request.Id)
                .ToListAsync(ct);
            context.ProductEntries.RemoveRange(entries);

            // ProductResidue
            var residues = await context.ProductResidues
                .Where(r => r.ProductTypeId == request.Id)
                .ToListAsync(ct);
            context.ProductResidues.RemoveRange(residues);

            // ProductType
            context.ProductTypes.Remove(productType);

            return await context.CommitTransactionAsync(ct);
        }
        catch
        {
            await context.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
