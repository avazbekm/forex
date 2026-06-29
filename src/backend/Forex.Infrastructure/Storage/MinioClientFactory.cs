namespace Forex.Infrastructure.Storage;

using Microsoft.Extensions.Options;
using Minio;

/// <summary>
/// Factory for creating MinIO clients with proper endpoint configuration
/// </summary>
public class ForexMinioClientFactory(IOptions<MinioStorageOptions> options)
{
    private readonly MinioStorageOptions _options = options.Value;

    /// <summary>
    /// Creates MinIO client for internal operations (bucket management, file operations)
    /// Uses internal Docker network endpoint
    /// </summary>
    public IMinioClient CreateInternalClient()
    {
        var uri = new Uri(_options.Endpoint.StartsWith("http")
            ? _options.Endpoint
            : $"http://{_options.Endpoint}");

        var builder = new MinioClient()
            .WithEndpoint(uri.Authority)
            .WithCredentials(_options.AccessKey, _options.SecretKey);

        if (uri.Scheme == Uri.UriSchemeHttps)
            builder.WithSSL();

        return builder.Build();
    }

    /// <summary>
    /// Creates MinIO client for presigned URL generation
    /// Uses public endpoint if specified, otherwise internal endpoint
    /// </summary>
    public IMinioClient CreatePublicClient(string? requestHost = null)
    {
        var publicEndpoint = DeterminePublicEndpoint(requestHost);

        var uri = new Uri(publicEndpoint.StartsWith("http")
            ? publicEndpoint
            : $"http://{publicEndpoint}");

        var builder = new MinioClient()
            .WithEndpoint(uri.Authority)
            .WithCredentials(_options.AccessKey, _options.SecretKey);

        if (_options.UseSsl || uri.Scheme == Uri.UriSchemeHttps)
            builder.WithSSL();

        return builder.Build();
    }

    public string DeterminePublicEndpoint(string? requestHost)
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicEndpoint))
            return _options.PublicEndpoint;

        if (!string.IsNullOrWhiteSpace(requestHost))
        {
            var hostParts = requestHost.Split(':');
            var hostname = hostParts[0];
            return $"{hostname}:{_options.PublicPort}";
        }

        return _options.Endpoint;
    }
}
