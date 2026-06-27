namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiProductEntries
{
    [Post("/api/product-entries")]
    Task<Response<long>> Create([Body] ProductEntryRequest request);

    [Put("/api/product-entries")]
    Task<Response<long>> Update([Body] ProductEntryRequest request);

    [Delete("/api/product-entries/{id}")]
    Task<Response<bool>> Delete(long id);

    [Post("/api/product-entries/filter")]
    Task<Response<List<ProductEntryResponse>>> Filter([Body] FilteringRequest request);

    [Post("/api/product-entries/image/upload-url")]
    Task<Response<PresignedUrlResponse>> GenerateUploadUrl([Body] GenerateUploadUrlRequest request);
}
