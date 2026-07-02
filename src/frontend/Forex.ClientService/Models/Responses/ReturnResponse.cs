namespace Forex.ClientService.Models.Responses;

public sealed record ReturnResponse
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public int TotalCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BaseAmount { get; set; }
    public string? Note { get; set; }

    public long CurrencyId { get; set; }
    public string? CurrencyCode { get; set; }

    public long OperationRecordId { get; set; }
    public OperationRecordResponse OperationRecord { get; set; } = default!;

    public long CustomerId { get; set; }
    public UserResponse Customer { get; set; } = default!;

    public ICollection<ReturnItemResponse> ReturnItems { get; set; } = default!;
}

public sealed record ReturnItemResponse
{
    public long Id { get; set; }
    public int BundleCount { get; set; }
    public int BundleItemCount { get; set; }
    public int TotalCount { get; set; }
    public int RestockCount { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }

    public long ReturnId { get; set; }

    public long ProductTypeId { get; set; }
    public ProductTypeResponse ProductType { get; set; } = default!;
}
