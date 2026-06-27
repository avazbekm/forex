namespace Forex.Wpf.Pages.Reports.ViewModels;

using Forex.Wpf.Common.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

public partial class FinishedStockReportView : UserControl
{
    private static readonly Brush ActiveBg = new SolidColorBrush(Colors.White);
    private static readonly Brush ActiveFg = new SolidColorBrush(Color.FromRgb(0x1B, 0x7A, 0x3E));
    private static readonly Brush InactiveFg = new SolidColorBrush(Color.FromRgb(0x7A, 0x86, 0x99));

    public FinishedStockReportView()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        RegisterFocusNavigation();
        RegisterGlobalShortcuts();
        SetView(showChart: false);
        UpdateMetric();
    }

    private void TableView_Click(object sender, RoutedEventArgs e) => SetView(showChart: false);
    private void ChartView_Click(object sender, RoutedEventArgs e) => SetView(showChart: true);

    private void MetricSum_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is FinishedStockReportViewModel vm) vm.StockByCount = false;
        UpdateMetric();
    }

    private void MetricCount_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is FinishedStockReportViewModel vm) vm.StockByCount = true;
        UpdateMetric();
    }

    private void StockSort_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FinishedStockReportViewModel vm) return;
        vm.StockDescending = !vm.StockDescending;
        icoStockSort.Kind = vm.StockDescending
            ? MaterialDesignThemes.Wpf.PackIconKind.SortDescending
            : MaterialDesignThemes.Wpf.PackIconKind.SortAscending;
    }

    private void UpdateMetric()
    {
        bool byCount = DataContext is FinishedStockReportViewModel { StockByCount: true };
        btnMetricCount.Background = byCount ? ActiveBg : Brushes.Transparent;
        btnMetricCount.Foreground = byCount ? ActiveFg : InactiveFg;
        btnMetricSum.Background = byCount ? Brushes.Transparent : ActiveBg;
        btnMetricSum.Foreground = byCount ? InactiveFg : ActiveFg;
    }

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
        FocusNavigator.RegisterElements(
            [
            txtSearch,
            btnLoad,
            btnClear,
            btnPreview,
            btnPrint,
            btnExport,
        ]);
    }

    private void RegisterGlobalShortcuts()
    {
        btnPrint.RegisterShortcut(Key.P, ModifierKeys.Control);
    }
}
