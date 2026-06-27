namespace Forex.Wpf.Windows;

using Forex.Wpf.Pages.Sales.ViewModels;
using System.Windows;

public partial class PaymentLinkWindow : Window
{
    private readonly PaymentLinkViewModel viewModel;

    public PaymentLinkWindow(PaymentLinkViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    public PaymentLinkResult? Result => viewModel.Result;

    private void OnCloseRequested(bool success)
    {
        DialogResult = success;
        Close();
    }
}
