namespace Forex.Domain.Entities.SemiProducts;

using Forex.Domain.Commons;
using Forex.Domain.Entities;

public class SemiProduct : Auditable
{
    public string? Name { get; set; }
    public string? NormalizedName { get; set; } = string.Empty;
    public string? ImagePath { get; set; }

    public long UnitMeasureId { get; set; }
    public UnitMeasure UnitMeasure { get; set; } = default!;
}
