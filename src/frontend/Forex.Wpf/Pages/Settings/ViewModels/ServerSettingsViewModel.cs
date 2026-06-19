namespace Forex.Wpf.Pages.Settings.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService.Services;
using Forex.Wpf.Pages.Common;
using System.Net.Http;

public partial class ServerSettingsViewModel : ViewModelBase
{
    private readonly ApiEndpointStore endpointStore;
    private bool isConnectionValid;

    public ServerSettingsViewModel(ApiEndpointStore endpointStore)
    {
        this.endpointStore = endpointStore;
        ServerUrl = endpointStore.BaseUrl;
    }

    [ObservableProperty] private string serverUrl = string.Empty;
    [ObservableProperty] private string statusText = string.Empty;

    [RelayCommand]
    private async Task TestAsync()
    {
        isConnectionValid = await TestConnectionAsync(showSuccess: true);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!isConnectionValid)
            isConnectionValid = await TestConnectionAsync(showSuccess: false);

        if (!isConnectionValid)
            return;

        endpointStore.SaveBaseUrl(ServerUrl);
        SuccessMessage = "Server URL saqlandi.";
    }

    partial void OnServerUrlChanged(string value)
    {
        isConnectionValid = false;
        StatusText = string.Empty;
    }

    private async Task<bool> TestConnectionAsync(bool showSuccess)
    {
        try
        {
            var baseUrl = ApiEndpointStore.Normalize(ServerUrl);
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync(new Uri(new Uri($"{baseUrl}/"), "api/auth/ping"));

            if (!response.IsSuccessStatusCode)
            {
                StatusText = $"Server javob berdi, lekin API noto'g'ri: {(int)response.StatusCode}";
                WarningMessage = StatusText;
                return false;
            }

            if (showSuccess)
            {
                ServerUrl = baseUrl;
                StatusText = "Ulanish muvaffaqiyatli.";
                SuccessMessage = StatusText;
            }

            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"Ulanib bo'lmadi: {ex.Message}";
            ErrorMessage = StatusText;
            return false;
        }
    }
}
