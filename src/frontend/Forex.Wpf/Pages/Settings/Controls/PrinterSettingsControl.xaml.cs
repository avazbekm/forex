namespace Forex.Wpf.Pages.Settings.Controls;

using Forex.Wpf.Common.Services;
using System.Linq;
using System.Printing;
using System.Windows.Controls;

public partial class PrinterSettingsControl : UserControl
{
    public PrinterSettingsControl()
    {
        InitializeComponent();
        DataContext = AppPreferences.Instance;

        printerCombo.ItemsSource = new LocalPrintServer()
            .GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections })
            .Select(q => q.FullName)
            .ToList();
    }
}
