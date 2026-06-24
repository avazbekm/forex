namespace Forex.Wpf.Pages.Settings.Controls;

using Forex.Wpf.Common.Services;
using System.Windows.Controls;

public partial class AppearanceSettingsControl : UserControl
{
    public AppearanceSettingsControl()
    {
        InitializeComponent();
        DataContext = AppPreferences.Instance;
    }
}
