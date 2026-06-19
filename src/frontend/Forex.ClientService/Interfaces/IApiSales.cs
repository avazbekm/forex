namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiSales
{
    [Post("/api/sales")]
    Task<Response<long?>> Create(SaleRequest request);

    [Put("/api/sales")]
    Task<Response<bool>> Update(SaleRequest request);

    [Delete("/api/sales/{id}")]
    Task<Response<bool>> Delete(long id);

    [Post("/api/sales/filter")]
    Task<IApiResponse<Response<List<SaleResponse>>>> Filter(FilteringRequest request);
    
    [Get("/api/sales")]
    Task<Response<List<SaleResponse>>> GetAll();
}
