namespace Forex.Wpf.Pages.Barcode.Views;

using Forex.Wpf.Common.Services;
using Forex.Wpf.Pages.Barcode.ViewModels;
using Forex.Wpf.Pages.Home;
using Forex.Wpf.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

public partial class BarcodePage : Page
{
    private static MainWindow Main => (MainWindow)Application.Current.MainWindow;
    private readonly BarcodePageViewModel vm;

    public BarcodePage()
    {
        InitializeComponent();
        vm = App.AppHost!.Services.GetRequiredService<BarcodePageViewModel>();
        DataContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += Page_Loaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        this.ResizeWindow(1280, 800);
        SetupScanBox();
        productList.SelectionChanged += (_, _) => FocusScan();
        FocusScan();
    }

    private void SetupScanBox()
    {
        if (tbxSearch.input is { } scan)
            scan.PreviewKeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;

                var code = scan.Text?.Trim();
                if (string.IsNullOrWhiteSpace(code) || BarcodeResolver.Resolve(vm.AvailableProducts, code) is null)
                    return;

                e.Handled = true;
                scan.Clear();
                vm.ScanCommand.Execute(code);
            };

        copiesBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            vm.PrintCommand.Execute(null);
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BarcodePageViewModel.IsPopupOpen)) return;

        if (vm.IsPopupOpen)
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                copiesBox.Focus();
                copiesBox.SelectAll();
            });
        else
            FocusScan();
    }

    private void FocusScan() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => tbxSearch.input?.Focus());

    private void ClosePopup_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender && vm.IsPopupOpen)
        {
            vm.ClosePopupCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationService?.CanGoBack == true)
            NavigationService.GoBack();
        else
            Main.NavigateTo(new HomePage());
    }
}
