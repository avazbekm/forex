namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiShops
{
    [Get("/api/shops")]
    Task<Response<List<ShopResponse>>> GetAllAsync();

    [Get("/api/shops/{id}")]
    Task<Response<ShopResponse>> GetById(long id);

    [Post("/api/shops")]
    Task<Response<long?>> Create([Body] ShopResponse dto);

    [Put("/api/shops")]
    Task<Response<bool>> Update([Body] ShopResponse dto);

    [Delete("/api/shops/{id}")]
    Task<Response<bool>> Delete(long id);

    [Post("/api/shops/filter")]
    Task<Response<List<ShopResponse>>> Filter(FilteringRequest request);
}