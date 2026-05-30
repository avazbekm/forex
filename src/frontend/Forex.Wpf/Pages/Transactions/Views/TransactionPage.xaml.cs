namespace Forex.Wpf.Pages.Transactions.Views;

using Forex.Wpf.Common.Services;
using Forex.Wpf.Pages.Home;
using Forex.Wpf.Pages.Transactions.ViewModels;
using Forex.Wpf.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public partial class TransactionPage : Page
{
    private static readonly Regex NumericRegex = new(@"^[0-9]+$", RegexOptions.Compiled);
    private static MainWindow Main => (MainWindow)Application.Current.MainWindow;
    private readonly TransactionPageViewModel vm;

    public TransactionPage()
    {
        InitializeComponent();
        vm = App.AppHost!.Services.GetRequiredService<TransactionPageViewModel>();
        DataContext = vm;

        Loaded += Page_Loaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        this.ResizeWindow(1100, 750);
        SetupUserComboBox();
        RegisterFocusNavigation();
        RegisterGlobalShortcuts();
    }

    private void SetupUserComboBox()
    {
        var combo = cbUser.InternalComboBox;
        combo.StaysOpenOnEdit = true;

        // Down/Up tugmalarini FocusNavigator'dan himoyalash
        combo.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Down or Key.Up && combo.IsDropDownOpen)
            {
                // ComboBox o'zi handle qilsin
                e.Handled = false;
            }
        };

        void setupEditBox()
        {
            if (combo.Template?.FindName("PART_EditableTextBox", combo) is not TextBox editBox) return;

            bool userTyping = false;

            editBox.PreviewKeyDown += (_, e) =>
            {
                userTyping = e.Key is not (Key.Down or Key.Up or Key.Enter or Key.Escape or Key.Tab or Key.Left or Key.Right);
            };

            editBox.TextChanged += (_, _) =>
            {
                if (!userTyping) return;
                userTyping = false;

                var text = editBox.Text?.Trim();
                vm.ApplyUserFilter(text);
                combo.IsDropDownOpen = true;
            };
        }

        combo.GotFocus += (_, _) =>
        {
            vm.ApplyUserFilter(null);
            combo.IsDropDownOpen = true;
        };

        if (combo.IsLoaded) setupEditBox();
        else combo.Loaded += (_, _) => setupEditBox();
    }

    private void RegisterGlobalShortcuts()
    {
        ShortcutAttacher.RegisterShortcut(
            targetButton: btnSubmit,
            key: Key.Enter,
            modifiers: ModifierKeys.Control
        );

        ShortcutAttacher.RegisterShortcut(
            targetButton: btnBack,
            key: Key.Escape
        );

        ShortcutAttacher.RegisterShortcut(
            targetButton: btnCancel,
            key: Key.Delete,
            modifiers: ModifierKeys.Control
        );
    }

    private void RegisterFocusNavigation()
    {
        FocusNavigator.RegisterElements(
        [
            cbUser.InternalComboBox,
            cbCurrency,
            tbKirim,
            cbPaymentMethod,
            tbChiqim,
            tbExchangeRate,
            tbDiscount,
            tbDescription,
            btnSubmit,
            btnCancel
        ]);
        FocusNavigator.SetFocusRedirect(btnSubmit, cbUser.InternalComboBox);
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
}