namespace Forex.Application.Features.Products.Products.Commands;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Interfaces;
using Forex.Domain.Entities.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

public record DeleteProductCommand(long Id) : IRequest<bool>;

public class DeleteProductCommandHandler(
    IAppDbContext context,
    IFileStorageService fileStorage)
    : IRequestHandler<DeleteProductCommand, bool>
{
    public async Task<bool> Handle(DeleteProductCommand request, CancellationToken ct)
    {
        var product = await context.Products
            .Include(p => p.ProductTypes)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(Product), nameof(request.Id), request.Id);

        // Barcha type'lar uchun savdoda qatnashganligini tekshirish
        var typeIds = product.ProductTypes.Select(t => t.Id).ToList();

        if (typeIds.Count > 0)
        {
            var hasSales = await context.SaleItems
                .AnyAsync(si => typeIds.Contains(si.ProductTypeId), ct);

            if (hasSales)
                throw new ForbiddenException(
                    "Bu mahsulot savdoda qatnashgan. O'chirib bo'lmaydi!");
        }

        await context.BeginTransactionAsync(ct);

        try
        {
            foreach (var typeId in typeIds)
            {
                // ProductEntry
                var entries = await context.ProductEntries
                    .Where(e => e.ProductTypeId == typeId)
                    .ToListAsync(ct);
                context.ProductEntries.RemoveRange(entries);

                // ProductResidue
                var residues = await context.ProductResidues
                    .Where(r => r.ProductTypeId == typeId)
                    .ToListAsync(ct);
                context.ProductResidues.RemoveRange(residues);
            }

            // ProductType'lar
            context.ProductTypes.RemoveRange(product.ProductTypes);

            // Image o'chirish
            if (!string.IsNullOrWhiteSpace(product.ImagePath))
            {
                try { await fileStorage.DeleteFileAsync(product.ImagePath, ct); }
                catch { /* Image o'chirishda xatolik bo'lsa davom etamiz */ }
            }

            // Product o'chirish
            context.Products.Remove(product);

            return await context.CommitTransactionAsync(ct);
        }
        catch
        {
            await context.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
