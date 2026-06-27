namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiUser
{
    [Get("/api/users")]
    Task<Response<List<UserResponse>>> GetAllAsync();

    [Post("/api/users/filter")]
    Task<Response<List<UserResponse>>> Filter(FilteringRequest request);

    [Get("/api/users/{id}")]
    Task<Response<UserResponse>> GetById(long id);

    [Post("/api/users")]
    Task<Response<long?>> Create([Body] UserRequest request);

    [Put("/api/users")]
    Task<Response<bool>> Update([Body] UserRequest request);

    [Delete("/api/users/{id}")]
    Task<Response<bool>> Delete(long id);
}
