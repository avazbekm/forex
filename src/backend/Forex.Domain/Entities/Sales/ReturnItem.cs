namespace Forex.Domain.Entities.Sales;

using Forex.Domain.Commons;
using Forex.Domain.Entities.Products;

public class ReturnItem : Auditable
{
    public int BundleCount { get; set; }
    public int BundleItemCount { get; set; }
    public int TotalCount { get; set; }
    public int RestockCount { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }

    public long ReturnId { get; set; }
    public Return Return { get; set; } = default!;

    public long ProductTypeId { get; set; }
    public ProductType ProductType { get; set; } = default!;
}
