namespace Forex.Wpf.Windows;

using Forex.ClientService.Services;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;

public partial class ServerUrlWindow : Window
{
    private readonly ApiEndpointStore endpointStore;
    private bool lastTestSucceeded;

    public ServerUrlWindow(ApiEndpointStore endpointStore)
    {
        InitializeComponent();
        this.endpointStore = endpointStore;
        tbUrl.Text = endpointStore.BaseUrl;
        tbUrl.SelectAll();
        tbUrl.Focus();
    }

    private async void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        lastTestSucceeded = await TestAsync(showSuccess: true);
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!lastTestSucceeded)
            lastTestSucceeded = await TestAsync(showSuccess: false);

        if (!lastTestSucceeded)
            return;

        endpointStore.SaveBaseUrl(tbUrl.Text);
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async Task<bool> TestAsync(bool showSuccess)
    {
        try
        {
            var baseUrl = ApiEndpointStore.Normalize(tbUrl.Text);
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync(new Uri(new Uri($"{baseUrl}/"), "api/auth/ping"));

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"Server javob berdi, lekin API noto'g'ri: {(int)response.StatusCode}", false);
                return false;
            }

            if (showSuccess)
                SetStatus("Ulanish muvaffaqiyatli.", true);

            tbUrl.Text = baseUrl;

            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Ulanib bo'lmadi: {ex.Message}", false);
            return false;
        }
    }

    private void SetStatus(string message, bool success)
    {
        tbStatus.Text = message;
        tbStatus.Foreground = success ? Brushes.ForestGreen : Brushes.Crimson;
    }
}
