namespace Forex.ClientService.Models.Responses;

using Forex.ClientService.Enums;

public sealed record SupplyResponse
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public SupplyPartyType PartyType { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public long UserId { get; set; }
    public UserResponse User { get; set; } = default!;
    public long CurrencyId { get; set; }
    public CurrencyResponse Currency { get; set; } = default!;
}
