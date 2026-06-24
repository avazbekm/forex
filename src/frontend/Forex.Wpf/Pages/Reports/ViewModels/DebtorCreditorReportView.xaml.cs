namespace Forex.Wpf.Pages.Reports.ViewModels;

using Forex.Wpf.Common.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;


/// <summary>
/// Interaction logic for DebtorCreditorReportView.xaml
/// </summary>
public partial class DebtorCreditorReportView : UserControl
{
    private static readonly Brush ActiveBg = new SolidColorBrush(Colors.White);
    private static readonly Brush ActiveFg = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
    private static readonly Brush InactiveFg = new SolidColorBrush(Color.FromRgb(0x7A, 0x86, 0x99));

    public DebtorCreditorReportView()
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

    private void DebtorSort_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DebtorCreditorReportViewModel vm) return;
        vm.DebtorDescending = !vm.DebtorDescending;
        icoDebtorSort.Kind = vm.DebtorDescending
            ? MaterialDesignThemes.Wpf.PackIconKind.SortDescending
            : MaterialDesignThemes.Wpf.PackIconKind.SortAscending;
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
                btnPreview,
                btnPrint,
                btnClear,
                btnExport,
            ]);

        FocusNavigator.FocusElement(txtSearch);
    }

    private void RegisterGlobalShortcuts()
    {
        btnPrint.RegisterShortcut(Key.P, ModifierKeys.Control);
    }

    private void TextBlock_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            // DataGridRow klassi System.Windows.Controls ichida
            var row = DataGridRow.GetRowContainingElement(textBlock);
            if (row != null)
            {
                textBlock.Text = (row.GetIndex() + 1).ToString();
            }
        }
    }
}
