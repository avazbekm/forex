namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiProducts
{
    [Get("/api/products")]
    Task<Response<List<ProductResponse>>> GetAllAsync();

    [Get("/api/products/{id}")]
    Task<Response<ProductResponse>> GetById(long id);

    [Post("/api/products")]
    Task<Response<long?>> Create([Body] ProductRequest request);

    [Put("/api/products")]
    Task<Response<bool>> Update([Body] ProductRequest request);

    [Delete("/api/products/{id}")]
    Task<Response<bool>> Delete(long id);

    [Post("/api/products/filter")]
    Task<Response<List<ProductResponse>>> Filter(FilteringRequest request);
}
