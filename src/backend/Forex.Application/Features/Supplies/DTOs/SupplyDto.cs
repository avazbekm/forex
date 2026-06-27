namespace Forex.Application.Features.Supplies.DTOs;

using Forex.Application.Features.Currencies.DTOs;
using Forex.Application.Features.Users.DTOs;
using Forex.Domain.Enums;

public sealed record SupplyDto
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public SupplyPartyType PartyType { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public long UserId { get; set; }
    public UserDto User { get; set; } = default!;
    public long CurrencyId { get; set; }
    public CurrencyDto Currency { get; set; } = default!;
}
