namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiUnitMeasures
{
    [Post("/api/unit-measures")]
    Task<Response<long?>> CreateAsync([Body] UnitMeasureRequest request);

    [Put("/api/unit-measures")]
    Task<Response<bool>> UpdateAsync([Body] UnitMeasureRequest request);

    [Put("/api/unit-measures/all")]
    Task<Response<bool>> SaveAllAsync(List<UnitMeasureRequest> dtoList);

    [Delete("/api/unit-measures/{id}")]
    Task<Response<bool>> DeleteAsync(long id);

    [Get("/api/unit-measures/{id}")]
    Task<Response<UnitMeasureResponse>> GetByIdAsync(long id);

    [Get("/api/unit-measures")]
    Task<Response<List<UnitMeasureResponse>>> GetAllAsync();
}