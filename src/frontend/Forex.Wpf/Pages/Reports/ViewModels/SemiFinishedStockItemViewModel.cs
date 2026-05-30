namespace Forex.Wpf.Pages.Reports.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

public partial class SemiFinishedStockItemViewModel : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string type = string.Empty;
    [ObservableProperty] private int bundleItemCount;
    [ObservableProperty] private int bundleCount;
    [ObservableProperty] private string unitMeasure = string.Empty;
    [ObservableProperty] private decimal totalCount;
    [ObservableProperty] private decimal purchasePrice;
    [ObservableProperty] private decimal expensePerItem;
    [ObservableProperty] private decimal costPrice;
    [ObservableProperty] private decimal totalAmount;
}
