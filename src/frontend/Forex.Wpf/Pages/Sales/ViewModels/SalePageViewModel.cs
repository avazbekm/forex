namespace Forex.Wpf.Pages.Sales.ViewModels;

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

    public SalePageViewModel()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(BeginDate) or nameof(EndDate))
            {
                CurrentPage = 1;
                _ = LoadSalesAsync();
            }
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
            LoadSalesAsync()
            );
    }

    private async Task LoadCustomersAsync()
    {
        FilteringRequest request = new()
        {
            Filters = new()
            {
                ["role"] = ["mijoz"]
            }
        };

        var response = await client.Users.Filter(request)
            .Handle(isLoading => IsLoading = isLoading);

        if (response.IsSuccess)
        {
            AvailableCustomers = mapper.Map<ObservableCollection<UserViewModel>>(response.Data);
            FilteredCustomers = new ObservableCollection<UserViewModel>(AvailableCustomers);
        }
        else
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
            else
            {
                TotalCount = response.Data?.Count ?? 0;
                TotalPages = TotalCount < PageSize ? CurrentPage : CurrentPage + 1;
            }
            
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
        }
        else
        {
            WarningMessage = response.Message ?? "Savdolarni yuklashda xatolik.";
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
        await LoadSalesAsync();
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
        CurrentPage = 1;
        _ = LoadSalesAsync();
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