namespace Forex.Application.Features.OperationRecords.DTOs;

public record OperationRecordTurnoverDto
{
    public decimal BeginBalance { get; set; }
    public decimal EndBalance { get; set; }
    public long SettlementCurrencyId { get; set; }
    public string? SettlementCurrencyCode { get; set; }
    public ICollection<OperationRecordDto> OperationRecords { get; set; } = default!;
}
