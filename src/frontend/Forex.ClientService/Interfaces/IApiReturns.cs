namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiReturns
{
    [Post("/api/returns")]
    Task<Response<long?>> Create(ReturnRequest request);

    [Put("/api/returns")]
    Task<Response<bool>> Update(ReturnRequest request);

    [Delete("/api/returns/{id}")]
    Task<Response<bool>> Delete(long id);

    [Post("/api/returns/filter")]
    Task<IApiResponse<Response<List<ReturnResponse>>>> Filter(FilteringRequest request);

    [Get("/api/returns")]
    Task<Response<List<ReturnResponse>>> GetAll();
}
