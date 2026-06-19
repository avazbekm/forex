namespace Forex.ClientService.Services;

using System.Text.Json;

public sealed class ApiEndpointStore
{
    private const string SettingsFileName = "settings.json";
    private readonly string settingsPath;
    private string baseUrl;

    public ApiEndpointStore(string defaultBaseUrl)
    {
        settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ForexApp",
            SettingsFileName);

        baseUrl = Normalize(LoadSavedBaseUrl() ?? defaultBaseUrl);
    }

    public string BaseUrl => baseUrl;
    public Uri BaseUri => new(baseUrl.EndsWith("/") ? baseUrl : $"{baseUrl}/");

    public void SetBaseUrl(string value)
    {
        baseUrl = Normalize(value);
    }

    public void SaveBaseUrl(string value)
    {
        SetBaseUrl(value);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(new ApiEndpointSettings { ApiBaseUrl = baseUrl }));
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Server URL bo'sh bo'lishi mumkin emas.", nameof(value));

        var input = value.Trim().TrimEnd('/');
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Server URL http yoki https bilan boshlanishi kerak.", nameof(value));
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        var apiIndex = segments.FindIndex(s => s.Equals("api", StringComparison.OrdinalIgnoreCase));

        if (apiIndex >= 0)
            segments = segments.Take(apiIndex).ToList();

        var builder = new UriBuilder(uri)
        {
            Path = string.Join('/', segments),
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    private string? LoadSavedBaseUrl()
    {
        try
        {
            if (!File.Exists(settingsPath))
                return null;

            var settings = JsonSerializer.Deserialize<ApiEndpointSettings>(File.ReadAllText(settingsPath));
            return string.IsNullOrWhiteSpace(settings?.ApiBaseUrl) ? null : settings.ApiBaseUrl;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ApiEndpointSettings
    {
        public string? ApiBaseUrl { get; set; }
    }
}
