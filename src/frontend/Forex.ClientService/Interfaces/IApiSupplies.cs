namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiSupplies
{
    [Get("/api/supplies")]
    Task<Response<List<SupplyResponse>>> GetAllAsync();

    [Post("/api/supplies")]
    Task<Response<long?>> Create([Body] SupplyRequest content);

    [Put("/api/supplies/{id}")]
    Task<Response<bool>> Update(long id, [Body] SupplyRequest content);

    [Delete("/api/supplies/{id}")]
    Task<Response<bool>> Delete(long id);
}
