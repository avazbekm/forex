namespace Forex.Application.Features.Sales.DTOs;

using Forex.Application.Features.OperationRecords.DTOs;
using Forex.Application.Features.Sales.SaleItems.DTOs;
using Forex.Application.Features.Users.DTOs;

public sealed record SaleDto
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public int TotalCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }

    public long CurrencyId { get; set; }
    public string? CurrencyCode { get; set; }

    public long OperationRecordId { get; set; }
    public OperationRecordForSaleDto OperationRecord { get; set; } = default!;

    public long CustomerId { get; set; }
    public UserForSaleDto Customer { get; set; } = default!;

    public ICollection<SaleItemForSaleDto> SaleItems { get; set; } = default!;
}
