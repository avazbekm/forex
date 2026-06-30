namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiProductTypes
{
    [Get("/api/product-types")]
    Task<Response<List<ProductTypeResponse>>> GetAll();

    [Post("/api/product-types/filter")]
    Task<Response<List<ProductTypeResponse>>> Filter(FilteringRequest request);

    [Get("/api/product-types/by-barcode/{code}")]
    Task<Response<ProductTypeResponse?>> GetByBarcode(string code);

    [Post("/api/product-types/generate-barcodes")]
    Task<Response<int>> GenerateBarcodes();

    [Post("/api/product-types")]
    Task<Response<long>> Create([Body] ProductTypeRequest request);

    [Put("/api/product-types")]
    Task<Response<bool>> Update([Body] ProductTypeRequest request);

    [Delete("/api/product-types/{id}")]
    Task<Response<bool>> Delete(long id);
}
