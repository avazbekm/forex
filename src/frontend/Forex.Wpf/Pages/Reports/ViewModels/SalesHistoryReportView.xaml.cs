namespace Forex.Wpf.Pages.Reports.ViewModels;

using Forex.Wpf.Common.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

public partial class SalesHistoryReportView : UserControl
{
    private static readonly Brush ActiveBg = new SolidColorBrush(Colors.White);
    private static readonly Brush ActiveFg = new SolidColorBrush(Color.FromRgb(0x3B, 0x5B, 0xDB));
    private static readonly Brush InactiveFg = new SolidColorBrush(Color.FromRgb(0x7A, 0x86, 0x99));

    public SalesHistoryReportView()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        RegisterFocusNavigation();
        RegisterGlobalShortcuts();
        SetView(showChart: false);
    }

    private void TableView_Click(object sender, RoutedEventArgs e) => SetView(showChart: false);
    private void ChartView_Click(object sender, RoutedEventArgs e) => SetView(showChart: true);

    private void SetView(bool showChart)
    {
        grid.Visibility = showChart ? Visibility.Collapsed : Visibility.Visible;
        chartPanel.Visibility = showChart ? Visibility.Visible : Visibility.Collapsed;

        btnChartView.Background = showChart ? ActiveBg : Brushes.Transparent;
        btnChartView.Foreground = showChart ? ActiveFg : InactiveFg;
        btnTableView.Background = showChart ? Brushes.Transparent : ActiveBg;
        btnTableView.Foreground = showChart ? InactiveFg : ActiveFg;
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
