namespace Forex.Wpf.Pages.Sales.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.ViewModels;

public partial class SaleItemViewModel : ViewModelBase
{
    public long Id { get; set; }
    public long SaleId { get; set; }
    public long ProductTypeId { get; set; }
    [ObservableProperty] private int? bundleCount;
    [ObservableProperty] private int? bundleItemCount;
    [ObservableProperty] private int? totalCount;
    [ObservableProperty] private decimal? unitPrice;
    [ObservableProperty] private decimal? costtPrice;
    [ObservableProperty] private decimal? benifit;
    [ObservableProperty] private decimal? amount;

    [ObservableProperty] private ProductViewModel product = default!;
    [ObservableProperty] private ProductTypeViewModel productType = default!;
    [ObservableProperty] private SaleViewModel sale = default!;

    #region Property Changes

    partial void OnUnitPriceChanged(decimal? value) => RecalculateTotalAmount();
    partial void OnBundleCountChanged(int? value) => ReCalculateTotalCount();
    partial void OnProductTypeChanged(ProductTypeViewModel value)
    {
        BundleItemCount = value?.BundleItemCount;
        ReCalculateTotalCount();
        if (value?.UnitPrice is > 0)
            UnitPrice = value.UnitPrice;
    }
    partial void OnTotalCountChanged(int? value) => RecalculateTotalAmount();

    #endregion Property Changes

    #region Private Helpers

    private void ReCalculateTotalCount()
    {
        if (ProductType is not null)
            TotalCount = ProductType.BundleItemCount * BundleCount;
    }

    private void RecalculateTotalAmount()
    {
        Amount = UnitPrice * TotalCount;
    }

    #endregion Private Helpers
}
