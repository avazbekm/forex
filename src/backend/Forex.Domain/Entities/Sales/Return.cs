namespace Forex.Domain.Entities.Sales;

using Forex.Domain.Commons;
using Forex.Domain.Entities;

public class Return : Auditable
{
    public DateTime Date { get; set; }
    public int TotalCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BaseAmount { get; set; }
    public string? Note { get; set; }

    public long CurrencyId { get; set; }
    public Currency Currency { get; set; } = default!;

    public long CustomerId { get; set; }
    public User Customer { get; set; } = default!;

    public long OperationRecordId { get; set; }
    public OperationRecord OperationRecord { get; set; } = default!;

    public ICollection<ReturnItem> ReturnItems { get; set; } = default!;
}
