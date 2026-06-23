namespace Forex.Application.Features.Sales.Commands;

using Forex.Application.Common.Exceptions;
using Forex.Application.Common.Extensions;
using Forex.Application.Common.Interfaces;
using Forex.Application.Features.Sales.SaleItems.Commands;
using Forex.Domain.Entities;
using Forex.Domain.Entities.Products;
using Forex.Domain.Entities.Sales;
using Forex.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text;

public record UpdateSaleCommand(
    long Id,
    DateTime Date,
    long CustomerId,
    decimal TotalAmount,
    string? Note,
    List<SaleItemCommand> SaleItems)
    : IRequest<bool>;

public class UpdateSaleCommandHandler(
    IAppDbContext context)
    : IRequestHandler<UpdateSaleCommand, bool>
{
    public async Task<bool> Handle(UpdateSaleCommand request, CancellationToken ct)
    {
        await context.BeginTransactionAsync(ct);

        try
        {
            var sale = await LoadSaleWithRelationsAsync(request.Id, ct);

            await RevertSaleEffectsAsync(sale, ct);

            await ApplyNewSaleDataAsync(sale, request, ct);

            return await context.CommitTransactionAsync(ct);
        }
        catch
        {
            await context.RollbackTransactionAsync(ct);
            throw;
        }
    }

    private async Task<Sale> LoadSaleWithRelationsAsync(long id, CancellationToken ct)
    {
        return await context.Sales
            .Include(s => s.SaleItems)
            .Include(s => s.Customer)
                .ThenInclude(u => u.Accounts)
            .Include(s => s.OperationRecord)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(Sale), nameof(id), id);
    }

    private async Task RevertSaleEffectsAsync(Sale sale, CancellationToken ct)
    {
        if (sale.OperationRecord is not null)
        {
            var account = await context.GetSettlementAccountAsync(sale.CustomerId, ct);
            account.Balance -= sale.OperationRecord.Amount * sale.OperationRecord.Rate;
        }

        var productTypeIds = sale.SaleItems.Select(si => si.ProductTypeId).Distinct().ToList();
        var productResidues = await LoadProductResiduesAsync(productTypeIds, ct);
        RestoreProductResidues(sale.SaleItems, productResidues);

        context.SaleItems.RemoveRange(sale.SaleItems);
        sale.SaleItems.Clear();
    }

    private async Task ApplyNewSaleDataAsync(Sale sale, UpdateSaleCommand request, CancellationToken ct)
    {
        var customer = await context.Users.FirstOrDefaultAsync(u => u.Id == request.CustomerId, ct)
            ?? throw new NotFoundException(nameof(User), nameof(request.CustomerId), request.CustomerId);

        var productTypeIds = request.SaleItems.Select(i => i.ProductTypeId).Distinct().ToList();
        var productResidues = await LoadProductResiduesAsync(productTypeIds, ct);

        sale.Date = request.Date.ToUtcSafe();
        sale.CustomerId = request.CustomerId;
        sale.CurrencyId = customer.SettlementCurrencyId;
        sale.TotalAmount = request.TotalAmount;
        sale.Note = request.Note;

        var saleItems = BuildSaleItems(request.SaleItems, productResidues, sale);

        DeductProductResidues(request.SaleItems, productResidues);

        CalculateSaleTotals(sale, saleItems);

        sale.SaleItems = saleItems;

        var currencyCode = await context.Currencies
            .Where(c => c.Id == sale.CurrencyId)
            .Select(c => c.Code)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var description = await GenerateDescriptionAsync(saleItems, currencyCode, ct);

        var (rate, _) = await context.ApplyToSettlementAsync(
            sale.CustomerId, sale.CurrencyId, 1m, -sale.TotalAmount, ct);

        var record = sale.OperationRecord ?? new OperationRecord();
        record.Amount = -sale.TotalAmount;
        record.Rate = rate;
        record.CurrencyId = sale.CurrencyId;
        record.Date = sale.Date;
        record.Description = description;
        record.Type = OperationType.Sale;
        record.UserId = sale.CustomerId;
        sale.OperationRecord = record;

        context.Sales.Update(sale);
    }

    private async Task<string> GenerateDescriptionAsync(List<SaleItem> saleItems, string currencyCode, CancellationToken ct)
    {
        var text = new StringBuilder();
        var productTypeIds = saleItems.Select(i => i.ProductTypeId).ToList();

        var productTypes = await context.ProductTypes
            .Include(pt => pt.Product)
            .Where(pt => productTypeIds.Contains(pt.Id))
            .ToListAsync(ct);

        foreach (var item in saleItems)
        {
            var productType = productTypes.FirstOrDefault(pt => pt.Id == item.ProductTypeId)
                ?? throw new NotFoundException(nameof(ProductType), nameof(item.ProductTypeId), item.ProductTypeId);

            text.AppendLine($"Kodi: {productType.Product.Code} ({productType.Type}), Soni: {item.TotalCount}, Narxi: {item.UnitPrice}, Jami: {item.Amount} {currencyCode}");
        }

        return text.ToString();
    }

    private async Task<List<ProductResidue>> LoadProductResiduesAsync(List<long> productTypeIds, CancellationToken ct)
    {
        return await context.ProductResidues
            .Include(p => p.ProductEntries)
            .Include(p => p.ProductType)
            .Where(p => productTypeIds.Contains(p.ProductTypeId))
            .ToListAsync(ct);
    }

    private List<SaleItem> BuildSaleItems(List<SaleItemCommand> commands, List<ProductResidue> residues, Sale sale)
    {
        var items = new List<SaleItem>();

        foreach (var cmd in commands.Where(c => c.BundleCount > 0))
        {
            var residue = residues.FirstOrDefault(r => r.ProductTypeId == cmd.ProductTypeId)
                ?? throw new NotFoundException(nameof(ProductResidue), nameof(cmd.ProductTypeId), cmd.ProductTypeId);

            var entry = residue.ProductEntries.OrderByDescending(e => e.Date).FirstOrDefault()
                ?? throw new NotFoundException(nameof(ProductEntry), nameof(residue.ProductTypeId), residue.ProductTypeId);

            var totalCount = cmd.BundleCount * entry.BundleItemCount;

            items.Add(new SaleItem
            {
                BundleCount = cmd.BundleCount,
                BundleItemCount = entry.BundleItemCount,
                TotalCount = totalCount,
                UnitPrice = cmd.UnitPrice,
                Amount = cmd.Amount,
                ProductTypeId = cmd.ProductTypeId,
                Sale = sale
            });
        }

        return items;
    }

    private void RestoreProductResidues(IEnumerable<SaleItem> saleItems, List<ProductResidue> residues)
    {
        foreach (var item in saleItems)
        {
            var residue = residues.FirstOrDefault(r => r.ProductTypeId == item.ProductTypeId)
                ?? throw new NotFoundException(nameof(ProductResidue), nameof(item.ProductTypeId), item.ProductTypeId);

            residue.Count += item.TotalCount;
        }
    }

    private void DeductProductResidues(List<SaleItemCommand> commands, List<ProductResidue> residues)
    {
        foreach (var cmd in commands.Where(c => c.BundleCount > 0))
        {
            var residue = residues.First(r => r.ProductTypeId == cmd.ProductTypeId);

            var entry = residue.ProductEntries.OrderByDescending(e => e.Date).FirstOrDefault()
                ?? throw new NotFoundException(nameof(ProductEntry), nameof(residue.ProductTypeId), residue.ProductTypeId);

            var totalCount = cmd.BundleCount * entry.BundleItemCount;

            if (residue.Count < totalCount)
                throw new ForbiddenException($"Do'konda yetarli mahsulot mavjud emas, jami mahsulot soni {residue.Count}");

            residue.Count -= totalCount;
        }
    }

    private static void CalculateSaleTotals(Sale sale, List<SaleItem> items)
    {
        sale.TotalCount = items.Sum(s => s.TotalCount);
    }
}
