namespace Forex.Wpf.Pages.Reports.ViewModels;

using Forex.ClientService;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Responses;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.ViewModels;
using MapsterMapper;
using System.Collections.ObjectModel;


public partial class CommonReportDataService : ViewModelBase
{
    private readonly ForexClient _client;
    private readonly IMapper _mapper;


    public ObservableCollection<UserViewModel> AvailableCustomers { get; } = [];
    public ObservableCollection<ProductViewModel> AvailableProducts { get; } = [];
    public ObservableCollection<CurrencyResponse> Currencies { get; } = [];

    private bool _isRefreshing;

    public CommonReportDataService(ForexClient client, IMapper mapper)
    {
        _client = client;
        _mapper = mapper;
    }

    public decimal BaseRate(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return 1;
        var currency = Currencies.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        if (currency is null) return 1;
        return currency.IsDefault || currency.ExchangeRate == 0 ? 1 : currency.ExchangeRate;
    }

    public async Task RefreshAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        try
        {
            await Task.WhenAll(
                LoadCustomersAsync(),
                LoadProductsAsync(),
                LoadCurrenciesAsync()
            );
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task LoadCurrenciesAsync()
    {
        var response = await _client.Currencies.GetAllAsync().Handle(isLoading => IsLoading = isLoading);
        if (response.IsSuccess && response.Data is not null)
        {
            Currencies.Clear();
            foreach (var c in response.Data) Currencies.Add(c);
        }
    }

    private async Task LoadCustomersAsync()
    {
        var request = new FilteringRequest
        {
            Filters = new()
            {
                ["role"] = ["in:Mijoz,Taminotchi,Vositachi"],
                ["accounts"] = ["include:currency"]
            }
        };

        var response = await _client.Users.Filter(request).Handle(isLoading => IsLoading = isLoading);
        if (response.IsSuccess)
        {
            var customers = _mapper.Map<List<UserViewModel>>(response.Data);
            AvailableCustomers.Clear();
            foreach (var c in customers) AvailableCustomers.Add(c);
        }
    }

    private async Task LoadProductsAsync()
    {
        var response = await _client.Products.GetAllAsync().Handle(isLoading => IsLoading = isLoading);
        if (response.IsSuccess)
        {
            var products = _mapper.Map<List<ProductViewModel>>(response.Data!);
            AvailableProducts.Clear();
            foreach (var p in products) AvailableProducts.Add(p);
        }
    }
}
