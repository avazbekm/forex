namespace Forex.Application.Features.Returns.ReturnItems.DTOs;

using Forex.Application.Features.Products.ProductTypes.DTOs;

public sealed record ReturnItemForReturnDto
{
    public long Id { get; set; }
    public int BundleCount { get; set; }
    public int BundleItemCount { get; set; }
    public int TotalCount { get; set; }
    public int RestockCount { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }

    public long ReturnId { get; set; }

    public long ProductTypeId { get; set; }
    public ProductTypeDto ProductType { get; set; } = default!;
}
