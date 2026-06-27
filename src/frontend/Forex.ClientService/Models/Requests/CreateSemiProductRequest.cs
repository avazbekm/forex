namespace Forex.ClientService.Models.Requests;

public sealed record CreateSemiProductRequest
{
    public string Name { get; set; } = string.Empty;
    public long UnitMeasureId { get; set; }
}
