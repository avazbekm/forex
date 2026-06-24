namespace Forex.Wpf.ViewModels;

using Forex.ClientService.Enums;

public class SaleHistoryItemViewModel
{
    public DateTime Date { get; set; }
    public string Customer { get; set; } = default!;
    public ProductionOrigin ProductionOrigin { get; set; }

    public string Code { get; set; } = default!;
    public string ProductName { get; set; } = default!;
    public string Type { get; set; } = default!;
    public int BundleCount { get; set; }
    public int BundleItemCount { get; set; }

    public int TotalCount { get; set; }
    public string UnitMeasure { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal BaseAmount { get; set; }
    public string? CurrencyCode { get; set; }
}
