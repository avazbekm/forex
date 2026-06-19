namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiProductResidues

{
    [Post("/api/product-residues/filter")]
    Task<Response<List<ProductResidueResponse>>> Filter(FilteringRequest request);
}
