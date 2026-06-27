namespace Forex.Wpf.Pages.Reports.ViewModels;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

/// <summary>
/// Interaction logic for CustomerSalesRatingView.xaml
/// </summary>
public partial class CustomerSalesRatingView : UserControl
{
    private static readonly Brush ActiveBg = new SolidColorBrush(Colors.White);
    private static readonly Brush ActiveFg = new SolidColorBrush(Color.FromRgb(0x67, 0x3A, 0xB7));
    private static readonly Brush InactiveFg = new SolidColorBrush(Color.FromRgb(0x7A, 0x86, 0x99));

    public CustomerSalesRatingView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            SetView(showChart: false);
            UpdateMetric();
        };
    }

    private void TableView_Click(object sender, RoutedEventArgs e) => SetView(showChart: false);
    private void ChartView_Click(object sender, RoutedEventArgs e) => SetView(showChart: true);

    private void RateCount_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomerSalesRatingViewModel vm) vm.RatingByAmount = false;
        UpdateMetric();
    }

    private void RateSum_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomerSalesRatingViewModel vm) vm.RatingByAmount = true;
        UpdateMetric();
    }

    private void RateSort_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CustomerSalesRatingViewModel vm) return;
        vm.RatingDescending = !vm.RatingDescending;
        icoRateSort.Kind = vm.RatingDescending
            ? MaterialDesignThemes.Wpf.PackIconKind.SortDescending
            : MaterialDesignThemes.Wpf.PackIconKind.SortAscending;
    }

    private void UpdateMetric()
    {
        bool byAmount = DataContext is CustomerSalesRatingViewModel { RatingByAmount: true };
        btnRateSum.Background = byAmount ? ActiveBg : Brushes.Transparent;
        btnRateSum.Foreground = byAmount ? ActiveFg : InactiveFg;
        btnRateCount.Background = byAmount ? Brushes.Transparent : ActiveBg;
        btnRateCount.Foreground = byAmount ? InactiveFg : ActiveFg;
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
}
