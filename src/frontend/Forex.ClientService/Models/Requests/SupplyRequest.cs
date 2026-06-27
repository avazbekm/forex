namespace Forex.ClientService.Models.Requests;

using Forex.ClientService.Enums;

public sealed record SupplyRequest
{
    public DateTime Date { get; set; }
    public SupplyPartyType PartyType { get; set; }
    public long UserId { get; set; }
    public decimal Amount { get; set; }
    public long CurrencyId { get; set; }
    public string? Description { get; set; }
}
