namespace Forex.ClientService.Interfaces;

using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Refit;

public interface IApiProcesses
{
    [Post("/api/processes")]
    Task<Response<long?>> CreateAsync(List<EntryToProcessRequest> request);

    [Put("/api/processes")]
    Task<Response<bool>> EditAsync(EntryToProcessRequest request);

    [Delete("/api/processes/{id}")]
    Task<Response<bool>> DeleteAsync(long id);

    [Post("/api/processes/filter")]
    Task<Response<List<InProcessResponse>>> Filter(FilteringRequest request);
}
