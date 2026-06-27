namespace Forex.Application.Features.Returns.ReturnItems.Commands;

public class ReturnItemCommand
{
    public int BundleCount { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }

    public long ProductTypeId { get; set; }
}
