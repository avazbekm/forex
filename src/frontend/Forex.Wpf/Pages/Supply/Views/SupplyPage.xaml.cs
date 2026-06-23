namespace Forex.Wpf.Pages.Supply.Views;

using Forex.ClientService.Enums;
using Forex.Wpf.Common.Services;
using Forex.Wpf.Pages.Home;
using Forex.Wpf.Pages.Supply.ViewModels;
using Forex.Wpf.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public partial class SupplyPage : Page
{
    private static readonly Regex NumericRegex = new(@"^[0-9]+$", RegexOptions.Compiled);
    private static MainWindow Main => (MainWindow)Application.Current.MainWindow;

    public SupplyPage()
    {
        InitializeComponent();
        DataContext = App.AppHost!.Services.GetRequiredService<SupplyPageViewModel>();

        Loaded += Page_Loaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        this.ResizeWindow(940, 640);
        ShortcutAttacher.RegisterShortcut(btnBack, Key.Escape);
    }

    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !NumericRegex.IsMatch(e.Text);
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationService?.CanGoBack == true)
            NavigationService.GoBack();
        else
            Main.NavigateTo(new HomePage());
    }

    private void BtnCreateUser_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SupplyPageViewModel vm)
            return;

        var isSupplier = vm.SelectedPartyType == SupplyPartyType.Supplier;

        var window = new UserWindow
        {
            Owner = Window.GetWindow(this),
            Role = isSupplier ? UserRole.Taminotchi : UserRole.Vositachi,
            AccountCurrencyId = vm.SelectedCurrency?.Id ?? 0,
            Title = isSupplier ? "Yangi ta'minotchi qo'shish" : "Yangi vositachi qo'shish"
        };

        if (window.ShowDialog() == true)
            vm.AddCreatedUser(window.user);
    }
}
