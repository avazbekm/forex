namespace Forex.ClientService.Models.Requests;

public sealed record ReturnRequest
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public long CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public List<ReturnItemRequest> ReturnItems { get; set; } = default!;
}

public sealed record ReturnItemRequest
{
    public int BundleCount { get; set; }
    public int TotalCount { get; set; }
    public int RestockCount { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }

    public long ProductTypeId { get; set; }
}
