namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiManufactory
{
    [Get("/api/manufactories")]
    Task<Response<List<ManufactoryResponse>>> GetAll();

    [Get("/api/manufactories/{id}")]
    Task<Response<ManufactoryResponse>> GetById(long id);

    [Post("/api/manufactories")]
    Task<Response<long?>> Create([Body] ManufactoryResponse dto);

    [Put("/api/manufactories")]
    Task<Response<bool>> Update([Body] ManufactoryResponse dto);

    [Delete("/api/manufactories/{id}")]
    Task<Response<bool>> Delete(long id);
}
