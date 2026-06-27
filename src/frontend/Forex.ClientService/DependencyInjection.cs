namespace Forex.ClientService;

using Forex.ClientService.Configuration;
using Forex.ClientService.Interfaces;
using Forex.ClientService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;

public static class DependencyInjection
{
    public static IServiceCollection AddClientServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient();
        services.AddSingleton<AuthStore>();

        services.AddSingleton<IFileStorageClient, FileStorageClient>();
        services.AddSingleton(_ => new ApiEndpointStore(config.GetValue<string>("ApiBaseUrl")!));

        services.AddTransient<AuthHeaderHandler>();
        services.AddTransient<BaseUrlHandler>();

        services.AddAllRefitClients();

        services.AddSingleton<ForexClient>();

        return services;
    }

    private static IServiceCollection AddAllRefitClients(this IServiceCollection services)
    {
        var assembly = typeof(IApiAuth).Assembly;
        var refitInterfaces = assembly.GetTypes()
            .Where(t => t.IsInterface && t.Name.StartsWith("IApi"))
            .ToList();

        foreach (var apiInterface in refitInterfaces)
        {
            services.AddRefitClient(apiInterface)
                .ConfigureHttpClient((sp, c) =>
                {
                    c.BaseAddress = sp.GetRequiredService<ApiEndpointStore>().BaseUri;
                    c.Timeout = TimeSpan.FromSeconds(10);
                })
                .AddHttpMessageHandler<BaseUrlHandler>()
                .AddHttpMessageHandler<AuthHeaderHandler>();
        }

        return services;
    }
}
