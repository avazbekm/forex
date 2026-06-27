namespace Forex.Application.Features.Sales.DTOs;

public record SaleDocumentSummaryDto
{
    public string SettlementCurrencyCode { get; set; } = string.Empty;
    public decimal PriorBalance { get; set; }
    public decimal SaleAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RemainingBalance { get; set; }
    public ICollection<SalePaymentGroupDto> Payments { get; set; } = [];
}

public record SalePaymentGroupDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal Amount { get; set; }
    public decimal SettlementAmount { get; set; }
    public string Methods { get; set; } = string.Empty;
}
