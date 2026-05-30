namespace Forex.ClientService.Models.Commons;

using System.Text.Json.Serialization;

public class Response<T>
{
    public T Data { get; set; } = default!;
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; }

    [JsonIgnore]
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
}

public class PagedResponse<T> : Response<T>
{
    public PagedListMetadata? Metadata { get; set; }
}