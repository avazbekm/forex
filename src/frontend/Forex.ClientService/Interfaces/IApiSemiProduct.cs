namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiSemiProducts
{
    [Get("/api/semi-products")]
    Task<Response<List<SemiProductResponse>>> GetAll();

    [Get("/api/semi-products/{id}")]
    Task<Response<SemiProductResponse>> GetById(long id);

    [Delete("/api/semi-products/{id}")]
    Task<Response<bool>> Delete(long id);

    [Post("/api/semi-products")]
    Task<Response<long?>> Create([Body] CreateSemiProductRequest request);

    [Multipart]
    [Post("/api/semi-products")]
    Task<Response<long?>> Create([AliasAs("ManufactoryId")] int manufactoryId,
                                [AliasAs("Name")] string name,
                                [AliasAs("Code")] int code,
                                [AliasAs("Measure")] string measure,
                                [AliasAs("Photo")] StreamPart photo);

    [Multipart]
    [Put("/api/semi-products/{id}")]
    Task<Response<long?>> Update(long id,
                                [AliasAs("ManufactoryId")] int manufactoryId,
                                [AliasAs("Name")] string name,
                                [AliasAs("Code")] int code,
                                [AliasAs("Measure")] string measure,
                                [AliasAs("Photo")] StreamPart photo);

}
