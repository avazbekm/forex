namespace Forex.Wpf.Pages.Settings.Controls;

using Forex.Wpf.Pages.Settings.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

public partial class ServerSettingsControl : UserControl
{
    public ServerSettingsControl()
    {
        InitializeComponent();
        DataContext = App.AppHost!.Services.GetRequiredService<ServerSettingsViewModel>();
    }
}
