namespace Forex.ClientService.Models.Responses;

public sealed record SaleItemResponse
{
    public long Id { get; set; }
    public int BundleCount { get; set; }
    public int BundleItemCount { get; set; }
    public int TotalCount { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }

    public long SaleId { get; set; }
    public SaleResponse Sale { get; set; } = default!;

    public long ProductTypeId { get; set; }
    public ProductTypeResponse ProductType { get; set; } = default!;
}