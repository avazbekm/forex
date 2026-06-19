namespace Forex.Domain.Entities;

using Forex.Domain.Commons;
using Forex.Domain.Enums;

public class Supply : Auditable
{
    public DateTime Date { get; set; }
    public SupplyPartyType PartyType { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }

    public long UserId { get; set; }
    public User User { get; set; } = default!;

    public long CurrencyId { get; set; }
    public Currency Currency { get; set; } = default!;
}
