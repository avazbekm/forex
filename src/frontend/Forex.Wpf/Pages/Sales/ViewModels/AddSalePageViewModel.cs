namespace Forex.Wpf.Pages.Sales.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.Wpf.Common.Interfaces;
using Forex.Wpf.Common.Services;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.ViewModels;
using Forex.Wpf.Windows;
using MapsterMapper;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public partial class AddSalePageViewModel : ViewModelBase
{
    private readonly ForexClient client;
    private readonly IMapper mapper;
    private readonly INavigationService navigation;
    private readonly SaleSessionService saleSession;
    private static readonly Dictionary<string, BitmapSource> _imageCache = [];
    private static readonly HttpClient _httpClient = new();

    private readonly Task? _initializationTask;

    public AddSalePageViewModel(ForexClient client, IMapper mapper, INavigationService navigation, SaleSessionService saleSession)
    {
        this.client = client;
        this.mapper = mapper;
        this.navigation = navigation;
        this.saleSession = saleSession;

        SaleItems = saleSession.CartItems;

        if (saleSession.TotalAmount.HasValue) TotalAmount = saleSession.TotalAmount;
        if (saleSession.FinalAmount.HasValue) FinalAmount = saleSession.FinalAmount;
        if (saleSession.Date.HasValue) Date = saleSession.Date.Value;
        if (!string.IsNullOrEmpty(saleSession.Note)) Note = saleSession.Note;
        if (saleSession.SelectedCustomer != null) Customer = saleSession.SelectedCustomer;

        CurrentSaleItem.PropertyChanged += SaleItemPropertyChanged;
        SaleItems.CollectionChanged += (s, e) => RecalculateTotals();

        _initializationTask = LoadDataAsync();
    }

    [ObservableProperty] private DateTime date = DateTime.Now;
    [ObservableProperty] private decimal? totalAmount;
    [ObservableProperty] private decimal? finalAmount;
    [ObservableProperty] private decimal? totalAmountWithUserBalance;
    [ObservableProperty] private string note = string.Empty;

    // Statistik ma'lumotlar
    [ObservableProperty] private int totalRowsCount;
    [ObservableProperty] private int distinctProductsCount;
    [ObservableProperty] private int totalBundlesCount;
    [ObservableProperty] private int totalQuantityCount;

    [ObservableProperty] private SaleItemViewModel currentSaleItem = new();
    [ObservableProperty] private ObservableCollection<SaleItemViewModel> saleItems = [];
    [ObservableProperty] private SaleItemViewModel? selectedSaleItem = default;

    [ObservableProperty] private UserViewModel? customer;
    [ObservableProperty] private ObservableCollection<UserViewModel> availableCustomers = [];
    [ObservableProperty] private ObservableCollection<ProductViewModel> availableProducts = [];

    // Filtrlanuvchi ro'yxatlar — XAML shu ikki xususiyatga bind qiladi
    [ObservableProperty] private ObservableCollection<UserViewModel> filteredCustomers = [];
    [ObservableProperty] private ICollectionView filteredProducts;
    [ObservableProperty] private string productSearchText;

    partial void OnProductSearchTextChanged(string value)
    {
        // Navigatsiya paytida filterni buzmaslik uchun:
        if (CurrentSaleItem?.Product != null)
        {
            var p = CurrentSaleItem.Product;
            // Agar matn maxsulot kodi yoki nomiga teng bo'lsa, demak bu tanlov (navigatsiya) natijasi
            if (string.Equals(value, p.Code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, p.Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        FilteredProducts?.Refresh();
    }

    private bool FilterProducts(object item)
    {
        if (string.IsNullOrWhiteSpace(ProductSearchText)) return true;
        if (item is not ProductViewModel p) return false;

        var search = ProductSearchText.Trim();
        return (p.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (p.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [ObservableProperty] private long editingSaleId = 0;
    [ObservableProperty] private bool isEditingItem;
    [ObservableProperty] private int originalItemIndex = -1;
    private SaleItemViewModel? _editingItemSnapshot;

    // ─────────────────────────────────────────────
    // Filter public methodlar — code-behind chaqiradi
    // ─────────────────────────────────────────────

    /// <summary>
    /// Mahsulotlarni Kod yoki Nomi bo'yicha filtrlaydi.
    /// null yoki bo'sh qiymat berilsa to'liq ro'yxat ko'rsatiladi.
    /// </summary>
    public void ApplyProductFilter(string? searchText)
    {
        ProductSearchText = searchText;
    }

    /// <summary>
    /// Mijozlarni Ismi bo'yicha filtrlaydi.
    /// null yoki bo'sh qiymat berilsa to'liq ro'yxat ko'rsatiladi.
    /// </summary>
    public void ApplyCustomerFilter(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            FilteredCustomers = AvailableCustomers;
            return;
        }

        var lower = searchText.Trim().ToLower();
        var results = AvailableCustomers
            .Where(c => c.Name?.ToLower().Contains(lower) == true)
            .ToList();

        FilteredCustomers = new ObservableCollection<UserViewModel>(results);
    }

    // ─────────────────────────────────────────────
    // AvailableProducts / AvailableCustomers o'zgarganda filterni yangilaymiz
    // ─────────────────────────────────────────────

    partial void OnAvailableProductsChanged(ObservableCollection<ProductViewModel> value)
    {
        if (value is null) return;
        FilteredProducts = CollectionViewSource.GetDefaultView(value);
        FilteredProducts.Filter = FilterProducts;
    }

    partial void OnAvailableCustomersChanged(ObservableCollection<UserViewModel> value)
    {
        FilteredCustomers = value;
    }

    // ─────────────────────────────────────────────
    // Data Loading
    // ─────────────────────────────────────────────

    private async Task EnsureInitializedAsync()
    {
        if (_initializationTask is not null)
            await _initializationTask;
    }

    private async Task LoadDataAsync()
    {
        await Task.WhenAll(
            LoadUsersAsync(),
            LoadProductsAsync()
        );
    }

    public async Task LoadUsersAsync()
    {
        FilteringRequest request = new()
        {
            Filters = new()
            {
                ["role"] = ["mijoz"],
                ["accounts"] = ["include:currency"]
            }
        };

        var response = await client.Users.Filter(request).Handle(isLoading => IsLoading = isLoading);
        if (response.IsSuccess)
        {
            AvailableCustomers = mapper.Map<ObservableCollection<UserViewModel>>(response.Data!);

            if (Customer != null)
            {
                var match = AvailableCustomers.FirstOrDefault(c => c.Id == Customer.Id);
                if (match != null) Customer = match;
            }
            else if (EditingSaleId == 0 && saleSession.SelectedCustomer != null)
            {
                var match = AvailableCustomers.FirstOrDefault(c => c.Id == saleSession.SelectedCustomer.Id);
                if (match != null) Customer = match;
            }
        }
        else
            ErrorMessage = response.Message ?? "Mahsulot turlarini yuklashda xatolik.";
    }

    public async Task LoadProductsAsync()
    {
        FilteringRequest request = new()
        {
            Filters = new()
            {
                ["ProductType"] = ["include:Product"]
            }
        };

        var response = await client.ProductResidues.Filter(request).Handle(isLoading => IsLoading = isLoading);

        if (!response.IsSuccess)
        {
            ErrorMessage = response.Message ?? "Mahsulotlarni yuklashda xatolik.";
            return;
        }

        var productResidues = mapper.Map<ObservableCollection<ProductResidueViewModel>>(response.Data!);

        var allTypes = productResidues.Select(pr =>
        {
            pr.ProductType.AvailableCount = pr.Count;

            var inCart = SaleItems.FirstOrDefault(i => i.ProductType?.Id == pr.ProductType.Id);
            if (inCart != null)
            {
                pr.ProductType.AvailableCount -= (inCart.TotalCount ?? 0);
                inCart.ProductType = pr.ProductType;
                inCart.Product = pr.ProductType.Product;
            }

            return pr.ProductType;
        })
        .Where(pt => pt is not null && pt.Product is not null)
        .ToList();

        var grouped = allTypes.GroupBy(pt => pt.Product.Id);
        var products = new ObservableCollection<ProductViewModel>();

        foreach (var group in grouped)
        {
            var product = group.First().Product;
            product.ProductTypes = new ObservableCollection<ProductTypeViewModel>(group);
            products.Add(product);
        }

        AvailableProducts = products;
    }

    public async Task LoadSaleForEditAsync(long saleId, bool notifyOnLoad = true)
    {
        await EnsureInitializedAsync();

        EditingSaleId = saleId;

        SaleItems = new ObservableCollection<SaleItemViewModel>();
        SaleItems.CollectionChanged += (s, e) => RecalculateTotals();

        FilteringRequest request = new()
        {
            Filters = new()
            {
                ["id"] = [saleId.ToString()],
                ["saleItems"] = ["include:productType.product"]
            }
        };

        var response = await client.Sales.Filter(request).Handle(isLoading => IsLoading = isLoading);

        if (!response.IsSuccess)
        {
            ErrorMessage = response.Message ?? "Savdoni yuklashda xatolik!";
            EditingSaleId = 0;
            return;
        }

        var sale = mapper.Map<SaleViewModel>(response.Data.First());

        Date = sale.Date;
        Note = sale.Note ?? string.Empty;

        var customer = AvailableCustomers.FirstOrDefault(c => c.Id == sale.CustomerId);
        if (customer is not null) Customer = customer;

        if (sale.SaleItems is not null)
        {
            foreach (var saleItemResponse in sale.SaleItems)
            {
                var productTypeResponse = saleItemResponse.ProductType;
                var productResponse = productTypeResponse?.Product;

                ProductViewModel? productVM = null;

                if (productTypeResponse != null)
                    productVM = AvailableProducts.FirstOrDefault(p => p.Id == productTypeResponse.ProductId);

                if (productVM == null && productResponse != null)
                {
                    productVM = mapper.Map<ProductViewModel>(productResponse);
                    productVM.ProductTypes ??= [];
                    AvailableProducts.Add(productVM);
                }

                ProductTypeViewModel? productTypeVM = null;

                if (productVM != null && productTypeResponse != null)
                {
                    productTypeVM = productVM.ProductTypes?.FirstOrDefault(pt => pt.Id == productTypeResponse.Id);

                    if (productTypeVM == null)
                    {
                        productTypeVM = mapper.Map<ProductTypeViewModel>(productTypeResponse);
                        productVM.ProductTypes ??= [];
                        productVM.ProductTypes.Add(productTypeVM);
                    }
                }

                if (productVM == null || productTypeVM == null) continue;

                var newItemVM = new SaleItemViewModel
                {
                    Product = productVM,
                    ProductType = productTypeVM,
                    BundleCount = saleItemResponse.BundleCount,
                    UnitPrice = saleItemResponse.UnitPrice,
                    Amount = saleItemResponse.Amount,
                    TotalCount = saleItemResponse.TotalCount
                };

                newItemVM.PropertyChanged += SaleItemPropertyChanged;
                SaleItems.Add(newItemVM);
            }
        }

        RecalculateTotals();
    }

    // ─────────────────────────────────────────────
    // Commands
    // ─────────────────────────────────────────────

    [RelayCommand]
    private async Task Add()
    {
        if (Date.Date > DateTime.Today)
        {
            WarningMessage = "Kelajakdagi sanani tanlab bo'lmaydi!";
            return;
        }

        if (CurrentSaleItem.Product is null ||
            CurrentSaleItem.BundleCount == null ||
            CurrentSaleItem.ProductType is null ||
            CurrentSaleItem.UnitPrice is null)
        {
            WarningMessage = "Mahsulot tanlanmagan yoki miqdor noto'g'ri!";
            return;
        }

        int needed = CurrentSaleItem.TotalCount ?? 0;

        bool isDuplicate = false;
        if (!IsEditingItem)
        {
            isDuplicate = CurrentSaleItem.ProductType.Id > 0
               ? SaleItems.Any(s => s.ProductType?.Id == CurrentSaleItem.ProductType.Id)
               : SaleItems.Any(s => s.ProductType?.Type == CurrentSaleItem.ProductType.Type
                                 && s.Product?.Id == CurrentSaleItem.Product?.Id);
        }

        if (isDuplicate)
        {
            WarningMessage = "Bu turdagi mahsulot allaqachon ro'yxatda bor!";
            return;
        }

        while (CurrentSaleItem.ProductType.AvailableCount < needed)
        {
            int currentStock = CurrentSaleItem.ProductType.AvailableCount;
            int bundleItemCount = CurrentSaleItem.ProductType.BundleItemCount ?? 1;
            int neededBundles = bundleItemCount > 0 ? (int)Math.Ceiling((double)needed / bundleItemCount) : needed;
            int stockBundles = bundleItemCount > 0 ? currentStock / bundleItemCount : currentStock;
            int shortfallBundles = Math.Max(0, neededBundles - stockBundles);

            var msgResult = MessageBox.Show(
                $"Mahsulot yetarli emas!\n\n" +
                $"So'ralmoqda:  {neededBundles} qop ({needed:N0} ta)\n" +
                $"Omborda:       {stockBundles} qop ({currentStock:N0} ta)\n" +
                $"Yetishmaydi:  {shortfallBundles} qop\n" +
                $"1 qopda:        {bundleItemCount} ta mahsulot\n\n" +
                $"Kirim qilmoqchimisiz?",
                "Yetarli emas",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (msgResult == MessageBoxResult.No) return;

            var window = new QuickProductEntryWindow(
                CurrentSaleItem.Product,
                CurrentSaleItem.ProductType,
                needed, currentStock, Date, client);

            if (Application.Current.MainWindow != null)
                window.Owner = Application.Current.MainWindow;

            if (window.ShowDialog() != true) return;

            CurrentSaleItem.ProductType.AvailableCount += window.EnteredCount;
        }

        SaleItemViewModel item = new()
        {
            Product = CurrentSaleItem.Product,
            ProductType = CurrentSaleItem.ProductType,
            BundleCount = CurrentSaleItem.BundleCount,
            UnitPrice = CurrentSaleItem.UnitPrice,
            Amount = CurrentSaleItem.Amount,
            TotalCount = CurrentSaleItem.TotalCount,
        };

        item.ProductType.AvailableCount -= (item.TotalCount ?? 0);
        item.PropertyChanged += SaleItemPropertyChanged;

        if (IsEditingItem)
        {
            if (OriginalItemIndex >= 0 && OriginalItemIndex <= SaleItems.Count)
                SaleItems.Insert(OriginalItemIndex, item);
            else
                SaleItems.Add(item);

            IsEditingItem = false;
            OriginalItemIndex = -1;
            _editingItemSnapshot = null;
        }
        else
        {
            SaleItems.Add(item);
        }

        ClearCurrentSaleItem();
        RecalculateTotals();
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedSaleItem is null) return;

        if (IsEditingItem)
        {
            WarningMessage = "Avval tahrirlashni yakunlang!";
            return;
        }

        bool hasCurrentData = CurrentSaleItem.Product is not null || CurrentSaleItem.BundleCount.HasValue;

        if (hasCurrentData)
        {
            var result = MessageBox.Show(
                "Hozirgi kiritilgan ma'lumotlar o'chib ketadi. Davom etmoqchimisiz?",
                "Ogohlantirish",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No) return;
        }

        SelectedSaleItem.ProductType.AvailableCount += (SelectedSaleItem.TotalCount ?? 0);

        CurrentSaleItem.PropertyChanged -= SaleItemPropertyChanged;

        try
        {
            _editingItemSnapshot = SelectedSaleItem;
            OriginalItemIndex = SaleItems.IndexOf(SelectedSaleItem);
            IsEditingItem = true;

            CurrentSaleItem.Product = SelectedSaleItem.Product;
            CurrentSaleItem.ProductType = SelectedSaleItem.ProductType;
            CurrentSaleItem.BundleCount = SelectedSaleItem.BundleCount;
            CurrentSaleItem.UnitPrice = SelectedSaleItem.UnitPrice;
            CurrentSaleItem.Amount = SelectedSaleItem.Amount;
            CurrentSaleItem.TotalCount = SelectedSaleItem.TotalCount;

            SaleItems.Remove(SelectedSaleItem);
            SelectedSaleItem = null;

            RecalculateTotals();
        }
        finally
        {
            CurrentSaleItem.PropertyChanged += SaleItemPropertyChanged;
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        if (!IsEditingItem || _editingItemSnapshot is null)
        {
            IsEditingItem = false;
            ClearCurrentSaleItem();
            return;
        }

        _editingItemSnapshot.ProductType.AvailableCount -= (_editingItemSnapshot.TotalCount ?? 0);

        if (OriginalItemIndex >= 0 && OriginalItemIndex <= SaleItems.Count)
            SaleItems.Insert(OriginalItemIndex, _editingItemSnapshot);
        else
            SaleItems.Add(_editingItemSnapshot);

        IsEditingItem = false;
        OriginalItemIndex = -1;
        _editingItemSnapshot = null;

        ClearCurrentSaleItem();
        RecalculateTotals();
    }

    [RelayCommand]
    private void DeleteItem(SaleItemViewModel item)
    {
        if (item is null) return;

        var result = MessageBox.Show(
            "Mahsulotni o'chirishni tasdiqlaysizmi?",
            "O'chirishni tasdiqlash",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.No) return;

        item.ProductType.AvailableCount += (item.TotalCount ?? 0);
        item.PropertyChanged -= SaleItemPropertyChanged;
        SaleItems.Remove(item);
        RecalculateTotals();
    }

    [ObservableProperty] private SaleItemViewModel? popupItem;
    [ObservableProperty] private bool isPopupOpen;

    [RelayCommand]
    private void ViewProduct(SaleItemViewModel? item)
    {
        if (item is null) return;
        PopupItem = item;
        IsPopupOpen = true;
    }

    [RelayCommand]
    private void ClosePopup()
    {
        IsPopupOpen = false;
        PopupItem = null;
    }

    [RelayCommand]
    private void ClearSale()
    {
        var result = MessageBox.Show(
            "Barcha kiritilgan ma'lumotlarni tozalashni xohlaysizmi?",
            "Tozalash",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            Clear();
            SuccessMessage = "Ma'lumotlar tozalandi.";
        }
    }

    [RelayCommand]
    private async Task Submit()
    {
        if (Date.Date > DateTime.Today)
        {
            WarningMessage = "Kelajakdagi sanani tanlab bo'lmaydi!";
            return;
        }

        if (SaleItems.Count == 0)
        {
            WarningMessage = "Hech qanday mahsulot kiritilmagan!";
            return;
        }

        if (Customer is null)
        {
            WarningMessage = "Mijoz tanlanmagan!";
            return;
        }

        SaleRequest request = new()
        {
            Date = Date == DateTime.Today ? DateTime.Now : Date,
            CustomerId = Customer?.Id ?? 0,
            TotalAmount = FinalAmount ?? 0,
            Note = Note,
            SaleItems = [.. SaleItems.Select(item => new SaleItemRequest
            {
                ProductTypeId = item.ProductType.Id,
                BundleCount = (int)item.BundleCount!,
                UnitPrice = (decimal)item.UnitPrice!,
                Amount = (decimal)item.Amount!
            })]
        };

        bool isSuccess;

        if (EditingSaleId > 0)
        {
            request.Id = EditingSaleId;
            var response = await client.Sales.Update(request).Handle(isLoading => IsLoading = isLoading);

            if (isSuccess = response.IsSuccess)
                SuccessMessage = "Savdo muvaffaqiyatli yangilandi!";
            else
                ErrorMessage = response.Message ?? "Savdoni yangilashda xatolik!";
        }
        else
        {
            var response = await client.Sales.Create(request).Handle(isLoading => IsLoading = isLoading);

            if (isSuccess = response.IsSuccess)
            {
                SuccessMessage = $"Savdo muvaffaqiyatli yuborildi. Mahsulotlar soni: {SaleItems.Count}";

                var result = MessageBox.Show(
                    "Savdo muvaffaqiyatli saqlandi!\n\nChop etishni xohlaysizmi?",
                    "Muvaffaqiyat",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    await ShowPrintPreview();
            }
            else
                ErrorMessage = response.Message ?? "Savdoni ro'yxatga olishda xatolik!";
        }

        if (isSuccess)
        {
            Clear();
            navigation.GoBack();
        }
    }

    private void Clear()
    {
        saleSession.ClearSession();
        SaleItems = saleSession.CartItems;

        Customer = null;
        TotalAmount = null;
        FinalAmount = null;
        Note = string.Empty;
        TotalAmountWithUserBalance = null;
        EditingSaleId = 0;
        IsEditingItem = false;
        OriginalItemIndex = -1;
        _editingItemSnapshot = null;
        ClearCurrentSaleItem();
        RecalculateTotals();
    }

    private void ClearCurrentSaleItem()
    {
        CurrentSaleItem.PropertyChanged -= SaleItemPropertyChanged;
        CurrentSaleItem = new SaleItemViewModel();
        CurrentSaleItem.PropertyChanged += SaleItemPropertyChanged;
    }

    // ─────────────────────────────────────────────
    // Property Changes
    // ─────────────────────────────────────────────

    private void SaleItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SaleItemViewModel.Amount))
            RecalculateTotals();
    }

    partial void OnEditingSaleIdChanged(long value)
    {
        IsEditing = value > 0;
    }

    partial void OnTotalAmountChanged(decimal? value)
    {
        if (EditingSaleId == 0) saleSession.TotalAmount = value;
    }

    partial void OnNoteChanged(string value)
    {
        if (EditingSaleId == 0) saleSession.Note = value;
    }

    partial void OnDateChanged(DateTime value)
    {
        if (EditingSaleId == 0) saleSession.Date = value;
    }

    partial void OnFinalAmountChanged(decimal? value)
    {
        if (EditingSaleId == 0) saleSession.FinalAmount = value;
        if (Customer is not null)
            TotalAmountWithUserBalance = Customer.Balance - FinalAmount;
    }

    partial void OnCustomerChanged(UserViewModel? value)
    {
        if (EditingSaleId == 0) saleSession.SelectedCustomer = value;
        RecalculateTotalAmountWithUserBalance();
    }

    // ─────────────────────────────────────────────
    // Private Helpers
    // ─────────────────────────────────────────────

    private void RecalculateTotals()
    {
        TotalAmount = SaleItems.Sum(x => x.Amount ?? 0);
        FinalAmount = TotalAmount;

        TotalRowsCount = SaleItems.Count;
        DistinctProductsCount = SaleItems.Select(x => x.Product?.Code).Distinct().Count();
        TotalBundlesCount = SaleItems.Sum(x => x.BundleCount ?? 0);
        TotalQuantityCount = SaleItems.Sum(x => x.TotalCount ?? 0);
    }

    private void RecalculateTotalAmountWithUserBalance()
    {
        if (Customer is not null)
            TotalAmountWithUserBalance = Customer.Balance - TotalAmount;
    }

    // ─────────────────────────────────────────────
    // Generate Print Preview
    // ─────────────────────────────────────────────

    public async Task ShowPrintPreview()
    {
        if (SaleItems == null || !SaleItems.Any())
        {
            MessageBox.Show("Ko'rsatish uchun ma'lumot yo'q.", "Eslatma", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var uniqueUrls = SaleItems
                .Select(i => i.Product.DisplayImagePath)
                .Where(url => !string.IsNullOrEmpty(url) && !_imageCache.ContainsKey(url))
                .Distinct()
                .ToList();

            if (uniqueUrls.Any())
            {
                var tasks = uniqueUrls.Select(async url =>
                {
                    var bitmap = await DownloadBitmapAsync(url);
                    if (bitmap != null) _imageCache[url] = bitmap;
                });
                await Task.WhenAll(tasks);
            }

            var fixedDoc = CreateFixedDocumentForPrint();

            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
            var shareButton = new Button
            {
                Content = "Telegram'da ulashish",
                Padding = new Thickness(15, 5, 15, 5),
                Background = new SolidColorBrush(Color.FromRgb(0, 136, 204)),
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };

            shareButton.Click += (s, e) =>
            {
                try
                {
                    string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string folder = Path.Combine(docs, "Forex", "Savdolar");
                    Directory.CreateDirectory(folder);

                    string customerName = (Customer?.Name ?? "Naqd").Replace(" ", "_");
                    string fileName = $"Savdo_{customerName}_{DateTime.Now:dd_MM_yyyy}.pdf";
                    string path = Path.Combine(folder, fileName);

                    SaveFixedDocumentToPdf(fixedDoc, path, 96);

                    if (File.Exists(path))
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                    }
                }
                catch (Exception ex) { MessageBox.Show($"Ulashishda xatolik: {ex.Message}"); }
            };

            toolbar.Children.Add(shareButton);
            var viewer = new DocumentViewer { Document = fixedDoc, Margin = new Thickness(8) };
            var layout = new DockPanel();
            DockPanel.SetDock(toolbar, Dock.Top);
            layout.Children.Add(toolbar);
            layout.Children.Add(viewer);

            var previewWindow = new Window
            {
                Title = "Sotuv cheki - Oldindan ko'rish",
                Width = 1050,
                Height = 850,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = layout,
                Owner = Application.Current.MainWindow
            };

            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xatolik: {ex.Message}");
        }
    }

    private FixedDocument CreateFixedDocumentForPrint()
    {
        var fixedDoc = new FixedDocument();
        double pageWidth = 793.7;
        double pageHeight = 1122.5;
        double marginTop = 40;
        double marginBottom = 40;
        double marginLeft = 30;
        double marginRight = 30;
        double tableWorkingWidth = pageWidth - marginLeft - marginRight;

        var groupedItems = SaleItems.OrderBy(i => i.Product.Code).GroupBy(i => i.Product.Code).ToList();
        var flatItems = groupedItems.SelectMany(g => g).ToList();

        decimal totalAmountSum = flatItems.Sum(i => i.Amount) ?? 0;
        double totalBundleCountSum = flatItems.Sum(i => i.BundleCount) ?? 0;
        double totalTotalCountSum = flatItems.Sum(i => i.TotalCount) ?? 0;

        string[] headers = { "T/r", "Rasm", "Kod", "Nomi", "Razmer", "Qop soni", "Jami soni", "Narxi", "Jami summa" };
        double[] finalWidths = { 35, 70, 60, 165, 60, 60, 70, 70, 143.7 };

        int maxRowsPerPage = 22;
        int totalPages = (int)Math.Ceiling((double)(flatItems.Count + 1) / maxRowsPerPage);
        int processedIndex = 0;
        int globalTr = 0;

        for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
        {
            var page = new FixedPage { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
            var gridContainer = new StackPanel { Margin = new Thickness(marginLeft, marginTop, marginRight, 0) };

            gridContainer.Children.Add(new TextBlock
            {
                Text = "Sotilgan mahsulotlar ro'yxati",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            });

            gridContainer.Children.Add(new TextBlock
            {
                Text = $"Mijoz: {Customer?.Name.ToUpper() ?? "Naqd"} | Sana: {Date:dd.MM.yyyy}",
                FontSize = 14,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var grid = new Grid { Width = tableWorkingWidth, HorizontalAlignment = HorizontalAlignment.Left };
            for (int i = 0; i < finalWidths.Length; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(finalWidths[i]) });

            AddRow(grid, true, 0, headers);
            int currentRow = 1;

            int pageCount = Math.Min(maxRowsPerPage, flatItems.Count - processedIndex);
            var pageItems = flatItems.Skip(processedIndex).Take(pageCount).ToList();
            var pageGroups = pageItems.GroupBy(i => i.Product.Code).ToList();

            foreach (var group in pageGroups)
            {
                int groupSize = group.Count();
                bool imageRendered = false;

                foreach (var item in group)
                {
                    globalTr++;
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    AddCellToGrid(grid, globalTr.ToString(), currentRow, 0, false, TextAlignment.Center);

                    if (!imageRendered)
                    {
                        var imageBorder = CreateImageCell(item.Product.DisplayImagePath);
                        Grid.SetRow(imageBorder, currentRow);
                        Grid.SetColumn(imageBorder, 1);
                        Grid.SetRowSpan(imageBorder, groupSize);
                        grid.Children.Add(imageBorder);
                        imageRendered = true;
                    }

                    AddCellToGrid(grid, item.Product.Code ?? "", currentRow, 2, false, TextAlignment.Left);
                    AddCellToGrid(grid, item.Product.Name ?? "", currentRow, 3, false, TextAlignment.Left);
                    AddCellToGrid(grid, item.ProductType.Type ?? "", currentRow, 4, false, TextAlignment.Center);
                    AddCellToGrid(grid, item.BundleCount?.ToString("N0") ?? "0", currentRow, 5, false, TextAlignment.Right);
                    AddCellToGrid(grid, item.TotalCount?.ToString("N0") ?? "0", currentRow, 6, false, TextAlignment.Right);
                    AddCellToGrid(grid, item.UnitPrice?.ToString("N2") ?? "0.00", currentRow, 7, false, TextAlignment.Right);
                    AddCellToGrid(grid, item.Amount?.ToString("N2") ?? "0.00", currentRow, 8, false, TextAlignment.Right);

                    currentRow++;
                }
            }

            processedIndex += pageItems.Count;

            if (pageIndex == totalPages - 1)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var totalLabel = CreateCell("Jami:", true, TextAlignment.Left);
                totalLabel.Padding = new Thickness(10, 5, 4, 5);
                Grid.SetRow(totalLabel, currentRow);
                Grid.SetColumn(totalLabel, 0);
                Grid.SetColumnSpan(totalLabel, 5);
                grid.Children.Add(totalLabel);

                AddCellToGrid(grid, totalBundleCountSum.ToString("N0"), currentRow, 5, true, TextAlignment.Right);
                AddCellToGrid(grid, totalTotalCountSum.ToString("N0"), currentRow, 6, true, TextAlignment.Right);

                var totalAmountCell = CreateCell(totalAmountSum.ToString("N2"), true, TextAlignment.Right);
                Grid.SetRow(totalAmountCell, currentRow);
                Grid.SetColumn(totalAmountCell, 7);
                Grid.SetColumnSpan(totalAmountCell, 2);
                if (totalAmountCell.Child is TextBlock tbSum)
                {
                    tbSum.FontSize = 14;
                    tbSum.Foreground = new SolidColorBrush(Color.FromRgb(0, 50, 150));
                }
                grid.Children.Add(totalAmountCell);
            }

            gridContainer.Children.Add(grid);
            page.Children.Add(gridContainer);

            var pageNum = new TextBlock
            {
                Text = $"{pageIndex + 1} - bet / {totalPages}",
                FontSize = 11,
                Foreground = Brushes.Gray
            };
            FixedPage.SetRight(pageNum, marginRight);
            FixedPage.SetBottom(pageNum, marginBottom);
            page.Children.Add(pageNum);

            var pc = new PageContent();
            ((IAddChild)pc).AddChild(page);
            fixedDoc.Pages.Add(pc);
        }

        return fixedDoc;
    }

    private async Task<BitmapSource?> DownloadBitmapAsync(string url)
    {
        try
        {
            byte[] data = await _httpClient.GetByteArrayAsync(url);
            using MemoryStream ms = new(data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.DecodePixelWidth = 150;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }

    private Border CreateImageCell(string imagePath)
    {
        var border = new Border
        {
            Width = 70,
            Height = 70,
            BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        if (!string.IsNullOrEmpty(imagePath) && _imageCache.TryGetValue(imagePath, out var bitmap))
        {
            var img = new Image { Source = bitmap, Stretch = Stretch.Uniform, Margin = new Thickness(2) };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            border.Child = img;
        }
        else
        {
            border.Child = new TextBlock
            {
                Text = "Rasm yo'q",
                FontSize = 8,
                Foreground = Brushes.LightGray,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        return border;
    }

    private void AddCellToGrid(Grid grid, string text, int row, int col, bool isHeader, TextAlignment align)
    {
        var cell = CreateCell(text, isHeader, align);
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        grid.Children.Add(cell);
    }

    private void AddRow(Grid grid, bool isHeader, int row, params string[] values)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextAlignment[] alignments =
        {
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Left,
            TextAlignment.Left, TextAlignment.Center, TextAlignment.Right,
            TextAlignment.Right, TextAlignment.Right, TextAlignment.Right
        };

        for (int i = 0; i < values.Length; i++)
        {
            TextAlignment finalAlignment = isHeader ? TextAlignment.Center : alignments[i];
            var cell = CreateCell(values[i], isHeader, finalAlignment);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }
    }

    private Border CreateCell(string text, bool isHeader, TextAlignment alignment = TextAlignment.Left)
    {
        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0.5),
            Background = isHeader ? new SolidColorBrush(Color.FromRgb(235, 235, 235)) : Brushes.White,
            Padding = new Thickness(4, 5, 4, 5)
        };

        var tb = new TextBlock
        {
            Text = text,
            FontSize = isHeader ? 13 : 12,
            FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
            TextAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center
        };

        border.Child = tb;
        return border;
    }

    private void SaveFixedDocumentToPdf(FixedDocument doc, string path, int dpi = 300)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            using var pdfDoc = new PdfSharp.Pdf.PdfDocument();

            foreach (var pageContent in doc.Pages)
            {
                var fixedPage = pageContent.GetPageRoot(false);
                if (fixedPage == null) continue;

                fixedPage.Measure(new Size(fixedPage.Width, fixedPage.Height));
                fixedPage.Arrange(new Rect(0, 0, fixedPage.Width, fixedPage.Height));
                fixedPage.UpdateLayout();

                double scale = dpi / 96.0;
                var bitmap = new RenderTargetBitmap(
                    (int)(fixedPage.Width * scale),
                    (int)(fixedPage.Height * scale),
                    dpi, dpi, PixelFormats.Pbgra32);

                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.PushTransform(new ScaleTransform(scale, scale));
                    dc.DrawRectangle(new VisualBrush(fixedPage), null, new Rect(0, 0, fixedPage.Width, fixedPage.Height));
                }
                bitmap.Render(dv);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                ms.Position = 0;

                var pdfPage = pdfDoc.AddPage();
                pdfPage.Width = PdfSharp.Drawing.XUnit.FromMillimeter(210);
                pdfPage.Height = PdfSharp.Drawing.XUnit.FromMillimeter(297);

                using var xgfx = PdfSharp.Drawing.XGraphics.FromPdfPage(pdfPage);
                using var ximg = PdfSharp.Drawing.XImage.FromStream(ms);

                double ratio = Math.Min(pdfPage.Width.Point / ximg.PointWidth, pdfPage.Height.Point / ximg.PointHeight);
                double w = ximg.PointWidth * ratio;
                double h = ximg.PointHeight * ratio;

                xgfx.DrawImage(ximg, (pdfPage.Width.Point - w) / 2, (pdfPage.Height.Point - h) / 2, w, h);
            }

            pdfDoc.Save(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PDF saqlashda xatolik: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}