namespace Forex.Wpf.Pages.Reports.ViewModels;

using Forex.Wpf.Common.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public partial class SalesHistoryReportView : UserControl
{
    public SalesHistoryReportView()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        RegisterFocusNavigation();
        RegisterGlobalShortcuts();
    }

    private void RegisterFocusNavigation()
    {
        FocusNavigator.RegisterElements([
            cbxCustomer,
            dtBegin.input,
            dtEnd.input,
            btnLoad,
            btnPreview,
            btnPrint,
            btnExport,
        ]);

        FocusNavigator.FocusElement(cbxCustomer);
    }

    private void RegisterGlobalShortcuts()
    {
        btnPrint.RegisterShortcut(Key.P, ModifierKeys.Control);
    }
}
