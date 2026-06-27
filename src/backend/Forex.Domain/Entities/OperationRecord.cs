namespace Forex.Domain.Entities;

using Forex.Domain.Commons;
using Forex.Domain.Entities.Sales;
using Forex.Domain.Enums;

public class OperationRecord : Auditable
{
    public OperationType Type { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public decimal Rate { get; set; }
    public string Description { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public User? User { get; set; }
    public long CurrencyId { get; set; }
    public Currency Currency { get; set; } = default!;

    public Sale? Sale { get; set; }
    public Return? Return { get; set; }
    public Transaction? Transaction { get; set; }
    public long? SupplyId { get; set; }
    public Supply? Supply { get; set; }
}
