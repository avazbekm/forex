namespace Forex.ClientService.Models.Responses;

using Forex.ClientService.Enums;

public sealed class UnlinkedPaymentResponse
{
    public long Id { get; set; }
    public decimal Amount { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal Discount { get; set; }
    public long CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public bool IsLinkedToSale { get; set; }
}
