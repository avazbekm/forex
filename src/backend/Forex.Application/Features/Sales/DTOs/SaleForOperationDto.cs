namespace Forex.Application.Features.Sales.DTOs;

public record SaleForOperationDto
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public int TotalCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }

    public long OperationRecordId { get; set; }
    public long CustomerId { get; set; }
}
