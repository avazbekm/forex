namespace Forex.ClientService.Models.Commons;

using System.Text.Json.Serialization;

public record PagedListMetadata
{
    public int TotalCount { get; init; }
    public int PageSize { get; init; }
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }

    // Alias properties for flexibility
    public int PageNumber
    {
        get => CurrentPage;
        init => CurrentPage = value;
    }

    public int PageCount
    {
        get => TotalPages;
        init => TotalPages = value;
    }

    // Alias for mapping if needed
    [JsonIgnore]
    public int Page => CurrentPage;
}
