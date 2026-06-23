namespace Forex.ClientService.Models.Responses;

public sealed record SaleResponse
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public int TotalCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }

    public long CurrencyId { get; set; }
    public string? CurrencyCode { get; set; }

    public long OperationRecordId { get; set; }
    public OperationRecordResponse OperationRecord { get; set; } = default!;

    public long CustomerId { get; set; }
    public UserResponse Customer { get; set; } = default!;

    public ICollection<SaleItemResponse> SaleItems { get; set; } = default!;
}