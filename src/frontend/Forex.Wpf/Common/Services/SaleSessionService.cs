namespace Forex.Wpf.Common.Services;

using Forex.Wpf.Pages.Sales.ViewModels;
using Forex.Wpf.ViewModels;
using System.Collections.ObjectModel;

public class SaleSessionService
{
    public ObservableCollection<SaleItemViewModel> CartItems { get; } = new();
    public SaleItemViewModel CurrentInputItem { get; set; } = new();

    public UserViewModel? SelectedCustomer { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? FinalAmount { get; set; }
    public string Note { get; set; } = string.Empty;

    public void ClearSession()
    {
        CartItems.Clear();
        CurrentInputItem = new();
        SelectedCustomer = null;
        TotalAmount = null;
        FinalAmount = null;
        Note = string.Empty;
    }
}
