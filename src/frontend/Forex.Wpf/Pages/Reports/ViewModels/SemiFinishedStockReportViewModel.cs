namespace Forex.Wpf.Pages.Reports.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Forex.ClientService;
using Forex.ClientService.Extensions;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.ViewModels;
using System.Collections.ObjectModel;

using Forex.ClientService.Models.Responses;

// SemiFinishedStockReportViewModel.cs
public partial class SemiFinishedStockReportViewModel : ViewModelBase
{
    private readonly ForexClient _client;
    private readonly CommonReportDataService _commonData;

    [ObservableProperty] private ObservableCollection<SemiFinishedStockItemViewModel> items = [];
    [ObservableProperty] private decimal totalSum;

    [ObservableProperty] private ObservableCollection<SemiProductResponse> availableSemiProducts = [];
    [ObservableProperty] private SemiProductResponse? selectedSemiProduct;

    public SemiFinishedStockReportViewModel(ForexClient client, CommonReportDataService commonData)
    {
        _client = client;
        _commonData = commonData;

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SelectedSemiProduct))
                _ = LoadStockAsync();
        };

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        await LoadSemiProductsAsync();
        await LoadStockAsync();
    }

    private async Task LoadSemiProductsAsync()
    {
        var response = await _client.SemiProduct.GetAll().Handle(l => IsLoading = l);
        if (response.IsSuccess)
        {
            AvailableSemiProducts = new ObservableCollection<SemiProductResponse>(response.Data);
        }
    }

    private async Task LoadStockAsync()
    {
        Items.Clear();

        var response = await _client.Manufactories.GetAll().Handle(l => IsLoading = l);
        
        if (!response.IsSuccess || response.Data == null)
        { 
            ErrorMessage = "Yarim tayyor mahsulotlar yuklanmadi"; 
            return; 
        }

        var allItems = response.Data
            .SelectMany(m => m.SemiProducts ?? [])
            .ToList();

        foreach (var s in allItems)
        {
            if (SelectedSemiProduct != null && s.SemiProductId != SelectedSemiProduct.Id) continue;

            Items.Add(new SemiFinishedStockItemViewModel
            {
                Name = s.SemiProduct?.Name ?? "-",
                // Type = s.ProductType?.Type ?? "-", // Not available
                // BundleItemCount = s.ProductType?.BundleItemCount ?? 0,
                // BundleCount = s.BundleCount,
                UnitMeasure = s.SemiProduct?.UnitMeasure?.Name ?? "-",
                TotalCount = s.Quantity,
                // PurchasePrice = s.PurchasePrice,
                // ExpensePerItem = s.ExpensePerItem,
                // CostPrice = s.CostPrice,
                // TotalAmount = s.TotalAmount
            });
        }
        
        TotalSum = Items.Sum(x => x.TotalAmount);
    }
}