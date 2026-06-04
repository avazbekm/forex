namespace Forex.Infrastructure.Web;

using Forex.Application.Common.Interfaces;
using Forex.Application.Common.Models;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

public class HttpPagingMetadataWriter(IHttpContextAccessor accessor) : IPagingMetadataWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Write(PagedListMetadata metadata)
    {
        var headers = accessor.HttpContext?.Response?.Headers;
        if (headers is null) return;

        headers["X-Pagination"] = JsonSerializer.Serialize(metadata, JsonOptions);
    }
}
