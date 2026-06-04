namespace Forex.ClientService.Models.Commons;

using System.Text.Json.Serialization;

public record PagedListMetadata
{
    [JsonPropertyName("page")]
    public int CurrentPage { get; init; }
    
    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }
    
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }
    
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; init; }
    
    [JsonIgnore]
    public bool HasNext => CurrentPage < TotalPages;
    
    [JsonIgnore]
    public bool HasPrevious => CurrentPage > 1;

    [JsonIgnore]
    public int Page => CurrentPage;

    [JsonIgnore]
    public int PageNumber => CurrentPage;

    [JsonIgnore]
    public int PageCount => TotalPages;
}
