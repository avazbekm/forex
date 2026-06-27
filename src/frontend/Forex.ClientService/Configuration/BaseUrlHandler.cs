namespace Forex.ClientService.Configuration;

using Forex.ClientService.Services;
using System.Net.Http;

// Har bir so'rovdan oldin manzilning sxema/host/port qismini ApiEndpointStore'dagi
// JORIY base URL ga almashtiradi. Shu sabab Settings'da server URL o'zgartirilib saqlangach,
// dasturni qayta ishga tushirmasdan darrov yangi serverga so'rov ketadi.
public sealed class BaseUrlHandler(ApiEndpointStore endpointStore) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null)
        {
            var baseUri = endpointStore.BaseUri;
            request.RequestUri = new UriBuilder(request.RequestUri)
            {
                Scheme = baseUri.Scheme,
                Host = baseUri.Host,
                Port = baseUri.IsDefaultPort ? -1 : baseUri.Port
            }.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
