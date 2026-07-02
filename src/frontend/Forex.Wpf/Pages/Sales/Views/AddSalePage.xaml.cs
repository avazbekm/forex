namespace Forex.Wpf.Pages.Sales.Views;

using Forex.Wpf.Common.Services;
using Forex.Wpf.Pages.Home;
using Forex.Wpf.Pages.Sales.ViewModels;
using Forex.Wpf.ViewModels;
using Forex.Wpf.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;

public partial class AddSalePage : Page
{
    private static MainWindow Main => (MainWindow)Application.Current.MainWindow;
    private readonly AddSalePageViewModel vm;

    // Guard flags — true bo'lsa LostFocus qayta ishga tushmaydi.
    // BeginInvoke ichida reset bo'ladi, ya'ni fokus to'liq qaytguncha flag saqlanib qoladi.
    private bool _customerGuard;
    private bool _productGuard;

    // Foydalanuvchi haqiqatdan yozayotganini aniqlash uchun flag (arrow/enter emas)
    private bool _productUserIsTyping;
    private bool _productTypeUserIsTyping;

    public AddSalePage()
    {
        InitializeComponent();
        vm = App.AppHost!.Services.GetRequiredService<AddSalePageViewModel>();
        DataContext = vm;

        Loaded += Page_Loaded;
        PreviewKeyDown += Page_PreviewKeyDown;
    }

    private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Back) return;
        var focused = Keyboard.FocusedElement;
        if (focused is TextBox or PasswordBox) return;
        if (focused is ComboBox { IsEditable: true }) return;
        e.Handled = true;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // MUHIM: Setup handlerlar FocusNavigator'dan OLDIN qo'shilishi kerak.
        // Sababini tushuntiraman: PreviewKeyDown — tunneling event, bir xil elementga
        // qo'shilgan handlerlar ro'yxatga olinish tartibida chaqiriladi.
        // Bizning validatsiya handler'imiz Enter ni ushlab qolishi kerak bo'lsa,
        // FocusNavigator'ning handler'idan oldin ro'yxatga olinishi shart.
        SetupCustomerComboBox();
        SetupProductComboBox();
        SetupProductTypeComboBox();
        SetupScanBox();

        RegisterFocusNavigation();
        RegisterGlobalShortcuts();
    }

    private void SetupScanBox()
    {
        var box = tbxScan.input;
        if (box is null) return;

        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;

            var code = box.Text?.Trim();
            box.Clear();

            if (!string.IsNullOrWhiteSpace(code))
                vm.ScanToCartCommand.Execute(code);

            box.Focus();
        };

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => box.Focus());
    }

    // ─────────────────────────────────────────────
    // CUSTOMER COMBOBOX
    // ─────────────────────────────────────────────

    private void SetupCustomerComboBox()
    {
        var combo = cbxCustomerName.InternalComboBox;
        combo.StaysOpenOnEdit = true;

        // Down/Up tugmalarini FocusNavigator'dan himoyalash
        combo.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Down or Key.Up && combo.IsDropDownOpen)
            {
                // ComboBox o'zi handle qilsin, FocusNavigator aralashmasin
                e.Handled = false; // WPF default behavior ishlaydi
            }
        };

        combo.GotFocus += (_, _) =>
        {
            vm.ApplyCustomerFilter(null);
            combo.IsDropDownOpen = true;
        };

        combo.LostFocus += CustomerComboBox_LostFocus;

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
                vm.ApplyCustomerFilter(text);
                combo.IsDropDownOpen = true;
            };
        }

        if (combo.IsLoaded) setupEditBox();
        else combo.Loaded += (_, _) => setupEditBox();
    }

    private void CustomerComboBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_customerGuard) return;

        var text = cbxCustomerName.Text?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            vm.Customer = null;
            vm.ApplyCustomerFilter(null);
            return;
        }

        if (cbxCustomerName.SelectedItem is UserViewModel sel &&
            sel.Name.Equals(text, StringComparison.OrdinalIgnoreCase))
        {
            vm.ApplyCustomerFilter(null);
            return;
        }

        // AvailableCustomers'dan qidirish
        var existing = vm.AvailableCustomers.FirstOrDefault(c =>
            c.Name.Equals(text, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            vm.Customer = existing;
            cbxCustomerName.SelectedItem = existing;
            vm.ApplyCustomerFilter(null);
            return;
        }

        // Noma'lum mijoz — foydalanuvchidan so'rash
        // _customerGuard = true saqlanib turadi, BeginInvoke ichida reset bo'ladi.
        // Shunday qilib MessageBox yopilganda va fokus qaytganda LostFocus qayta chaqirilmaydi.
        _customerGuard = true;

        var confirm = MessageBox.Show(
            $"Mijoz '{text}' topilmadi. Yangi mijoz yaratilishini istaysizmi?",
            "Yangi mijoz",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            var newCustomer = OpenCreateCustomerWindow(text);
            if (newCustomer is not null)
            {
                _customerGuard = false;
                _ = SelectCreatedCustomerAsync(newCustomer.Id);
                return;
            }
        }

        // "Yo'q" bosildi yoki oyna bekor qilindi — fokusni qaytaramiz.
        // Guard flag BeginInvoke ICHIDA reset bo'ladi, chunki fokus qaytguncha
        // yana bir LostFocus yonishi mumkin.
        RestoreFocusWithSelectAll(cbxCustomerName, onComplete: () => _customerGuard = false);
    }

    private async Task SelectCreatedCustomerAsync(long id)
    {
        await vm.LoadUsersAsync();

        var full = vm.AvailableCustomers.FirstOrDefault(c => c.Id == id);
        if (full is not null)
        {
            vm.Customer = full;
            cbxCustomerName.SelectedItem = full;
        }
    }

    // ─────────────────────────────────────────────
    // PRODUCT COMBOBOX
    // ─────────────────────────────────────────────

    private void SetupProductComboBox()
    {
        var combo = cbxProduct.combo;
        if (combo is null) return;

        combo.StaysOpenOnEdit = true;

        // Bu handler FocusNavigator'dan OLDIN qo'shiladi.
        // Enter/Tab bosilganda validatsiya o'tkaziladi; noto'g'ri bo'lsa e.Handled=true
        // qilinadi va FocusNavigator hech narsani bilmaydi.
        combo.PreviewKeyDown += ProductComboBox_PreviewKeyDown;

        combo.GotFocus += (_, _) =>
        {
            vm.ApplyProductFilter(null);
            combo.IsDropDownOpen = true;
        };

        combo.LostFocus += ProductComboBox_LostFocus;

        void setupEditBox()
        {
            if (combo.Template?.FindName("PART_EditableTextBox", combo) is not TextBox editBox) return;

            editBox.PreviewKeyDown += (_, e) =>
            {
                _productUserIsTyping = e.Key is not (Key.Down or Key.Up or Key.Enter or Key.Escape or Key.Tab or Key.Left or Key.Right);
            };

            editBox.TextChanged += (_, _) =>
            {
                if (!_productUserIsTyping) return;
                _productUserIsTyping = false;

                var text = editBox.Text?.Trim();
                if (vm.CurrentSaleItem is not null)
                    vm.CurrentSaleItem.Product = null;
                combo.SelectedItem = null;
                combo.SelectedIndex = -1;
                vm.ApplyProductFilter(text);
                combo.IsDropDownOpen = true;
            };
        }

        if (combo.IsLoaded) setupEditBox();
        else combo.Loaded += (_, _) => setupEditBox();
    }

    private void ProductComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Tab)) return;

        var combo = cbxProduct.combo;

        // Dropdown ochiq bo'lsa: faqat yopamiz.
        // Agar SelectedItem allaqachon mavjud bo'lsa (arrow bilan tanlangan),
        // FocusNavigator oldinga o'tishiga ruxsat beramiz.
        if (combo.IsDropDownOpen)
        {
            combo.IsDropDownOpen = false;
            if (combo.SelectedItem is ProductViewModel selectedProduct)
                combo.Text = GetProductDisplayText(selectedProduct);
            if (combo.SelectedItem is null)
                e.Handled = true;
            return;
        }

        var text = combo.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (combo.SelectedItem is ProductViewModel selectedItem)
        {
            combo.Text = GetProductDisplayText(selectedItem);
            return;
        }

        // Aniq moslik qidiramiz
        var match = vm.AvailableProducts.FirstOrDefault(p =>
            p.Code?.Equals(text, StringComparison.OrdinalIgnoreCase) == true ||
            p.Name?.Equals(text, StringComparison.OrdinalIgnoreCase) == true);

        if (match is not null)
        {
            vm.CurrentSaleItem.Product = match;
            combo.SelectedItem = match;
            vm.ApplyProductFilter(null);
            combo.Text = GetProductDisplayText(match);
            return;
        }

        // Mos kelmadi — fokusni ushlab qolamiz
        e.Handled = true;
        combo.IsDropDownOpen = true;
    }

    private void ProductComboBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_productGuard) return;

        var combo = cbxProduct.combo;
        var text = combo.Text?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            combo.SelectedItem = null;
            combo.Text = string.Empty;
            if (vm.CurrentSaleItem is not null) vm.CurrentSaleItem.Product = null;
            vm.ApplyProductFilter(string.Empty);
            return;
        }

        if (combo.SelectedItem is ProductViewModel selectedProduct)
        {
            vm.ApplyProductFilter(null);
            combo.Text = GetProductDisplayText(selectedProduct);
            return;
        }

        var match = vm.AvailableProducts.FirstOrDefault(p =>
            p.Code?.Equals(text, StringComparison.OrdinalIgnoreCase) == true ||
            p.Name?.Equals(text, StringComparison.OrdinalIgnoreCase) == true);

        if (match is not null)
        {
            vm.CurrentSaleItem.Product = match;
            combo.SelectedItem = match;
            vm.ApplyProductFilter(null);
            combo.Text = GetProductDisplayText(match);
            return;
        }

        _productGuard = true;
        RestoreFocusWithSelectAll(combo, keepDropdownOpen: true, onComplete: () => _productGuard = false);
    }

    // ─────────────────────────────────────────────
    // PRODUCT TYPE COMBOBOX
    // ─────────────────────────────────────────────

    private void SetupProductTypeComboBox()
    {
        var combo = cbxProductType.combo;
        if (combo is null) return;

        combo.StaysOpenOnEdit = true;

        combo.PreviewKeyDown += ProductTypeComboBox_PreviewKeyDown;

        combo.LostFocus += ProductTypeComboBox_LostFocus;

        combo.DropDownOpened += (_, _) =>
        {
            if (combo.SelectedItem is not null && vm.CurrentSaleItem?.Product?.ProductTypes?.Count == 1)
            {
                combo.IsDropDownOpen = false;
            }
        };

        void setupEditBox()
        {
            if (combo.Template?.FindName("PART_EditableTextBox", combo) is not TextBox editBox) return;

            editBox.GotKeyboardFocus += (_, _) =>
            {
                if (!combo.IsDropDownOpen)
                    combo.IsDropDownOpen = true;
            };

            editBox.PreviewKeyDown += (_, e) =>
            {
                _productTypeUserIsTyping = e.Key is not (Key.Down or Key.Up or Key.Enter or Key.Escape or Key.Tab or Key.Left or Key.Right);
            };

            editBox.TextChanged += (_, _) =>
            {
                if (!_productTypeUserIsTyping) return;
                _productTypeUserIsTyping = false;
                combo.IsDropDownOpen = true;
            };
        }

        if (combo.IsLoaded) setupEditBox();
        else combo.Loaded += (_, _) => setupEditBox();
    }

    private void ProductTypeComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Tab)) return;

        var combo = cbxProductType.combo;

        if (combo.IsDropDownOpen)
        {
            combo.IsDropDownOpen = false;
            if (combo.SelectedItem is null)
                e.Handled = true;
            return;
        }

        var text = combo.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (combo.SelectedItem is not null) return;

        var types = vm.CurrentSaleItem?.Product?.ProductTypes;
        if (types is null)
        {
            e.Handled = true;
            return;
        }

        var match = types.FirstOrDefault(pt =>
            pt.Type?.Equals(text, StringComparison.OrdinalIgnoreCase) == true);

        if (match is not null)
        {
            vm.CurrentSaleItem!.ProductType = match;
            combo.SelectedItem = match;
            return;
        }

        e.Handled = true;
        combo.IsDropDownOpen = true;
    }

    private void ProductTypeComboBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var combo = cbxProductType.combo;
        var text = combo.Text?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            if (vm.CurrentSaleItem is not null) vm.CurrentSaleItem.ProductType = null;
            return;
        }

        if (combo.SelectedItem is not null) return;

        var types = vm.CurrentSaleItem?.Product?.ProductTypes;
        if (types is null) return;

        var match = types.FirstOrDefault(pt =>
            pt.Type?.Equals(text, StringComparison.OrdinalIgnoreCase) == true);

        if (match is not null)
        {
            vm.CurrentSaleItem!.ProductType = match;
            combo.SelectedItem = match;
            return;
        }

        combo.Text = "";
        if (vm.CurrentSaleItem is not null) vm.CurrentSaleItem.ProductType = null;
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    private static string GetProductDisplayText(ProductViewModel product)
    {
        if (!string.IsNullOrWhiteSpace(product.Code))
            return product.Code;

        return product.Name ?? string.Empty;
    }

    /// <summary>
    /// Fokusni belgilangan elementga qaytaradi va barcha matnni tanlaydi.
    /// onComplete — fokus qaytganidan KEYIN chaqiriladi (guard flagni reset qilish uchun).
    /// </summary>
    private void RestoreFocusWithSelectAll(Control target, bool keepDropdownOpen = false, Action? onComplete = null)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            target.Focus();

            if (target is ComboBox combo)
            {
                if (keepDropdownOpen) combo.IsDropDownOpen = true;

                if (combo.Template?.FindName("PART_EditableTextBox", combo) is TextBox editBox)
                {
                    editBox.Focus();
                    editBox.SelectAll();
                }
            }
            else if (target is TextBox tb)
            {
                tb.SelectAll();
            }

            onComplete?.Invoke();
        });
    }

    // ─────────────────────────────────────────────
    // NAVIGATION
    // ─────────────────────────────────────────────

    private void RegisterFocusNavigation()
    {
        FocusNavigator.RegisterElements([
            date.input,
            cbxCustomerName.InternalComboBox,
            tbxNote,
            tbxScan.input,
            cbxProduct.combo,
            cbxProductType.combo,
            tbxBundle.input,
            tbxUnitPrice.input,
            btnAdd,
            btnSubmit
        ]);

        FocusNavigator.SetFocusRedirect(btnAdd, tbxScan.input);
    }

    private void RegisterGlobalShortcuts()
    {
        btnBack.RegisterShortcut(Key.Escape);
        btnSubmit.RegisterShortcut(Key.Enter, ModifierKeys.Control);
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationService?.CanGoBack == true)
            NavigationService.GoBack();
        else
            Main.NavigateTo(new HomePage());
    }

    private UserViewModel? OpenCreateCustomerWindow(string name)
    {
        var dialog = new UserWindow();
        dialog.txtName.Text = name;
        return dialog.ShowDialog() == true ? dialog.user : null;
    }

    private void ClosePopup_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Agar bosilgan element overlay'ning o'zi bo'lsa (Border emas), yopamiz
        if (e.OriginalSource == sender && DataContext is AddSalePageViewModel viewModel && viewModel.IsPopupOpen)
        {
            viewModel.ClosePopupCommand.Execute(null);
            e.Handled = true;
        }
    }
}
