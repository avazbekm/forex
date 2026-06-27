namespace Forex.Wpf.Pages.Reports.ViewModels;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

/// <summary>
/// Interaction logic for CustomerTurnoverReportView.xaml
/// </summary>
public partial class CustomerTurnoverReportView : UserControl
{
    private static readonly Brush ActiveBg = new SolidColorBrush(Colors.White);
    private static readonly Brush ActiveFg = new SolidColorBrush(Color.FromRgb(0x39, 0x49, 0xAB));
    private static readonly Brush InactiveFg = new SolidColorBrush(Color.FromRgb(0x7A, 0x86, 0x99));

    public CustomerTurnoverReportView()
    {
        InitializeComponent();
        Loaded += (_, _) => SetView(showChart: false);
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
}
