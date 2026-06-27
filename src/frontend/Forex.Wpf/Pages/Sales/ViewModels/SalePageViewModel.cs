namespace Forex.Wpf.Pages.Sales.ViewModels;

using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Commons;
using Forex.Wpf.Common.Interfaces;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.ViewModels;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;

public partial class SalePageViewModel : ViewModelBase, INavigationAware
{
    private readonly ForexClient client = App.AppHost!.Services.GetRequiredService<ForexClient>();
    private readonly IMapper mapper = App.AppHost!.Services.GetRequiredService<IMapper>();
    private bool _suppressCustomerReload;

    public SalePageViewModel()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(BeginDate) or nameof(EndDate))
                _ = OnDateRangeChangedAsync();
        };

        _ = LoadDataAsync();
    }

    [ObservableProperty] private ObservableCollection<SaleViewModel> sales = [];
    [ObservableProperty] private ObservableCollection<UserViewModel> availableCustomers = [];
    [ObservableProperty] private ObservableCollection<UserViewModel> filteredCustomers = [];
    [ObservableProperty] private SaleViewModel? selectedSale;

    [ObservableProperty] private DateTime beginDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime endDate = DateTime.Today;
    [ObservableProperty] private UserViewModel? selectedCustomer;
    [ObservableProperty] private string customerSearchText = string.Empty;

    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private int totalPages = 1;
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private int pageSize = 20;
    [ObservableProperty] private ObservableCollection<object> visiblePageNumbers = [];
    
    [ObservableProperty] private decimal totalSalesAmount;
    [ObservableProperty] private int totalItemsSold;
    [ObservableProperty] private string currencyBreakdown = string.Empty;

    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;

    partial void OnCurrentPageChanged(int value) => UpdateVisiblePageNumbers();
    partial void OnTotalPagesChanged(int value) => UpdateVisiblePageNumbers();
    partial void OnCustomerSearchTextChanged(string value) => ApplyCustomerFilter();

    private void UpdateVisiblePageNumbers()
    {
        var pages = new List<object>();

        if (TotalPages <= 7)
        {
            for (int i = 1; i <= TotalPages; i++)
                pages.Add(i);
        }
        else
        {
            pages.Add(1);

            if (CurrentPage > 4)
            {
                pages.Add("...");
            }

            int start = Math.Max(2, CurrentPage - 1);
            int end = Math.Min(TotalPages - 1, CurrentPage + 1);

            if (CurrentPage < 5)
            {
                end = 5;
            }
            else if (CurrentPage > TotalPages - 4)
            {
                start = TotalPages - 4;
            }

            for (int i = start; i <= end; i++)
            {
                pages.Add(i);
            }

            if (CurrentPage < TotalPages - 3)
            {
                pages.Add("...");
            }

            pages.Add(TotalPages);
        }

        VisiblePageNumbers = new ObservableCollection<object>(pages);
    }
    
    private void ApplyCustomerFilter()
    {
        if (string.IsNullOrWhiteSpace(CustomerSearchText))
        {
            FilteredCustomers = new ObservableCollection<UserViewModel>(AvailableCustomers);
            return;
        }

        var filtered = AvailableCustomers.Where(c => 
            Forex.Wpf.Common.Services.TransliterationHelper.ContainsIgnoreScript(c.Name, CustomerSearchText) ||
            Forex.Wpf.Common.Services.TransliterationHelper.ContainsIgnoreScript(c.Phone, CustomerSearchText));

        FilteredCustomers = new ObservableCollection<UserViewModel>(filtered);
    }

    #region Load Data

    private async Task LoadDataAsync()
    {
        await Task.WhenAll(
            LoadCustomersAsync(),
            LoadSalesAsync(),
            LoadSalesSummaryAsync()
            );
    }

    private async Task OnDateRangeChangedAsync()
    {
        CurrentPage = 1;
        await LoadCustomersAsync();
        await Task.WhenAll(LoadSalesAsync(), LoadSalesSummaryAsync());
    }

    private async Task LoadCustomersAsync()
    {
        FilteringRequest request = new()
        {
            Page = 0,
            PageSize = 0,
            Filters = new()
            {
                ["date"] = [$">={BeginDate:o}", $"<{EndDate.AddDays(1):o}"],
                ["customer"] = ["include"]
            }
        };

        var response = await client.Sales.Filter(request)
            .Handle(isLoading => IsLoading = isLoading);

        if (response.IsSuccess && response.Data is not null)
        {
            var customers = response.Data
                .Where(s => s.Customer is not null)
                .Select(s => s.Customer!)
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .OrderBy(c => c.Name)
                .ToList();

            AvailableCustomers = mapper.Map<ObservableCollection<UserViewModel>>(customers);

            _suppressCustomerReload = true;
            var keepId = SelectedCustomer?.Id;
            SelectedCustomer = keepId is null ? null : AvailableCustomers.FirstOrDefault(c => c.Id == keepId);
            _suppressCustomerReload = false;

            ApplyCustomerFilter();
        }
        else if (!response.IsSuccess)
        {
            ErrorMessage = response.Message ?? "Mijozlarni yuklashda xatolik.";
        }
    }

    private async Task LoadSalesAsync()
    {
        FilteringRequest request = new()
        {
            Page = CurrentPage,
            PageSize = PageSize,
            SortBy = "date",
            Descending = true,
            Filters = new()
            {
                ["date"] = [$">={BeginDate:o}", $"<{EndDate.AddDays(1):o}"],
                ["customer"] = ["include"],
                ["saleItems"] = ["include:productType.product"]
            }
        };

        if (SelectedCustomer is not null)
        {
            request.Filters["customerId"] = [SelectedCustomer.Id.ToString()];
        }

        var response = await client.Sales.Filter(request)
            .HandleWithPagination(isLoading => IsLoading = isLoading);

        if (response.IsSuccess)
        {
            Sales = mapper.Map<ObservableCollection<SaleViewModel>>(response.Data);
            
            if (response.Metadata is not null)
            {
                TotalCount = response.Metadata.TotalCount;
                TotalPages = response.Metadata.TotalPages;
            }
            else if (TotalCount == 0)
            {
                TotalCount = response.Data?.Count ?? 0;
                TotalPages = PageSize > 0
                    ? Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize))
                    : 1;
            }
            
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
        }
        else
        {
            WarningMessage = response.Message ?? "Savdolarni yuklashda xatolik.";
        }
    }

    private async Task LoadSalesSummaryAsync()
    {
        FilteringRequest request = new()
        {
            Page = 0,
            PageSize = 0,
            Filters = new()
            {
                ["date"] = [$">={BeginDate:o}", $"<{EndDate.AddDays(1):o}"]
            }
        };

        if (SelectedCustomer is not null)
        {
            request.Filters["customerId"] = [SelectedCustomer.Id.ToString()];
        }

        var response = await client.Sales.Filter(request)
            .Handle();

        if (response.IsSuccess && response.Data is not null)
        {
            TotalSalesAmount = response.Data.Sum(s => s.BaseAmount);
            TotalItemsSold = response.Data.Sum(s => s.TotalCount);
            CurrencyBreakdown = string.Join("  |  ", response.Data
                .GroupBy(s => string.IsNullOrWhiteSpace(s.CurrencyCode) ? "—" : s.CurrencyCode)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}: {g.Sum(x => x.TotalAmount):N2}"));
            TotalCount = response.Data.Count;
            TotalPages = PageSize > 0
                ? Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize))
                : 1;
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task PreviewSale(SaleViewModel? sale)
    {
        if (sale is null) return;

        IsLoading = true;
        try
        {
            var printVm = App.AppHost!.Services.GetRequiredService<AddSalePageViewModel>();
            await printVm.LoadSaleForEditAsync(sale.Id, notifyOnLoad: false);
            await printVm.ShowPrintPreview();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task FilterSales()
    {
        CurrentPage = 1;
        await Task.WhenAll(
            LoadSalesAsync(),
            LoadSalesSummaryAsync()
        );
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedCustomer = null;
        CustomerSearchText = string.Empty;
        BeginDate = DateTime.Today.AddDays(-7);
        EndDate = DateTime.Today;
    }

    [RelayCommand]
    private async Task ExportToExcel()
    {
        FilteringRequest request = new()
        {
            Page = 0,
            PageSize = 0,
            SortBy = "date",
            Descending = true,
            Filters = new()
            {
                ["date"] = [$">={BeginDate:o}", $"<{EndDate.AddDays(1):o}"],
                ["customer"] = ["include"],
                ["saleItems"] = ["include:productType.product"]
            }
        };

        if (SelectedCustomer is not null)
            request.Filters["customerId"] = [SelectedCustomer.Id.ToString()];

        var response = await client.Sales.Filter(request).Handle(isLoading => IsLoading = isLoading);

        if (!response.IsSuccess || response.Data is null)
        {
            ErrorMessage = response.Message ?? "Ma'lumotlarni yuklashda xatolik.";
            return;
        }

        var list = mapper.Map<List<SaleViewModel>>(response.Data);

        if (list.Count == 0)
        {
            MessageBox.Show("Excelga eksport qilish uchun ma'lumot yo'q.", "Eslatma", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel fayllari (*.xlsx)|*.xlsx",
            FileName = $"Savdolar_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.xlsx"
        };

        if (dialog.ShowDialog() != true)
            return;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Savdolar");
        string[] headers = ["T/r", "Sana", "Mijoz", "Jami dona", "Summa", "Izoh"];

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Range(1, 1, 1, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);

        var row = 2;
        var index = 1;
        foreach (var sale in list)
        {
            ws.Cell(row, 1).Value = index++;
            ws.Cell(row, 2).Value = sale.Date.ToString("dd.MM.yyyy");
            ws.Cell(row, 3).Value = sale.Customer?.Name ?? "-";
            ws.Cell(row, 4).Value = sale.TotalCount;
            ws.Cell(row, 5).Value = sale.TotalAmount;
            ws.Cell(row, 6).Value = sale.Note;
            row++;
        }

        ws.Cell(row, 3).Value = "Jami:";
        ws.Cell(row, 4).Value = list.Sum(s => s.TotalCount);
        ws.Cell(row, 5).Value = list.Sum(s => s.TotalAmount);
        ws.Range(row, 3, row, 5).Style.Font.SetBold();
        ws.Columns().AdjustToContents();
        workbook.SaveAs(dialog.FileName);
        MessageBox.Show("Excel fayl muvaffaqiyatli saqlandi.", "Tayyor", MessageBoxButton.OK, MessageBoxImage.Information);
    }



    [RelayCommand]
    private async Task GoToFirstPage()
    {
        if (CurrentPage == 1) return;
        CurrentPage = 1;
        await LoadSalesAsync();
    }

    [RelayCommand]
    private async Task GoToPreviousPage()
    {
        if (!CanGoPrevious) return;
        CurrentPage--;
        await LoadSalesAsync();
    }

    [RelayCommand]
    private async Task GoToNextPage()
    {
        if (!CanGoNext) return;
        CurrentPage++;
        await LoadSalesAsync();
    }

    [RelayCommand]
    private async Task GoToLastPage()
    {
        if (CurrentPage == TotalPages) return;
        CurrentPage = TotalPages;
        await LoadSalesAsync();
    }

    [RelayCommand]
    private async Task GoToPage(object? parameter)
    {
        if (parameter is int page)
        {
            if (page < 1 || page > TotalPages || page == CurrentPage) return;
            CurrentPage = page;
            await LoadSalesAsync();
        }
    }

    [RelayCommand]
    private async Task Delete(SaleViewModel value)
    {
        if (value is null)
            return;

        var result = MessageBox.Show(
            $"Savdoni o'chirishni tasdiqlaysizmi?\n\nMijoz: {value.Customer?.Name}\nSana: {value.Date:dd.MM.yyyy}\nSumma: {value.TotalAmount:N2} so'm\nMahsulotlar soni: {value.SaleItems?.Count ?? 0}",
            "O'chirishni tasdiqlash",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.No)
            return;

        var response = await client.Sales.Delete(value.Id)
            .Handle(isLoading => IsLoading = isLoading);

        if (response.IsSuccess)
        {
            Sales.Remove(value);
            SuccessMessage = "Savdo muvaffaqiyatli o'chirildi";
            await LoadSalesAsync();
        }
        else
        {
            ErrorMessage = response.Message ?? "Savdoni o'chirishda xatolik";
        }
    }

    #endregion

    #region Property Changes

    partial void OnSelectedCustomerChanged(UserViewModel? value)
    {
        if (_suppressCustomerReload) return;
        CurrentPage = 1;
        _ = Task.WhenAll(
            LoadSalesAsync(),
            LoadSalesSummaryAsync()
        );
    }

    #endregion

    #region Private Helpers

    public void OnNavigatedTo()
    {
        _ = LoadDataAsync();
    }

    public void OnNavigatedFrom()
    {
    }

    #endregion Private Helpers
}