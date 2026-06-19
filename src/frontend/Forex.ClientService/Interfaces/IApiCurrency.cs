namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiCurrency
{
    [Post("/api/currencies")]
    Task<Response<long?>> CreateAsync([Body] CurrencyRequest request);

    [Put("/api/currencies")]
    Task<Response<bool>> UpdateAsync([Body] CurrencyRequest request);

    [Put("/api/currencies/all")]
    Task<Response<bool>> SaveAllAsync(List<CurrencyRequest> dtoList);

    [Delete("/api/currencies/{id}")]
    Task<Response<bool>> DeleteAsync(long id);

    [Get("/api/currencies/{id}")]
    Task<Response<CurrencyResponse>> GetByIdAsync(long id);

    [Get("/api/currencies")]
    Task<Response<List<CurrencyResponse>>> GetAllAsync();
}
