namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiTransactions
{
    [Post("/api/transactions")]
    Task<Response<long?>> CreateAsync(TransactionRequest request);

    [Put("/api/transactions")]
    Task<Response<bool>> Update(TransactionRequest request);

    [Delete("/api/transactions/{id}")]
    Task<Response<bool>> Delete(long id);

    [Get("/api/transactions")]
    Task<Response<List<TransactionResponse>>> GetAll();

    [Post("/api/transactions/filter")]
    Task<Response<List<TransactionResponse>>> Filter(FilteringRequest request);

    [Get("/api/transactions/unlinked")]
    Task<Response<List<UnlinkedPaymentResponse>>> GetUnlinked(long userId, DateTime date, long? saleId = null);

    [Post("/api/transactions/link-to-sale")]
    Task<Response<bool>> LinkToSale(LinkPaymentsToSaleRequest request);
}