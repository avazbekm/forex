namespace Forex.ClientService.Models.Requests;

public sealed record LinkPaymentsToSaleRequest
{
    public long SaleId { get; set; }
    public List<long> TransactionIds { get; set; } = [];
    public DateTime? DueDate { get; set; }
}
