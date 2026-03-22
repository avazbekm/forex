namespace Forex.Wpf.Pages.Sales.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.Wpf.Common.Interfaces;
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
    private static readonly Dictionary<string, BitmapSource> _imageCache = new();
    private static readonly HttpClient _httpClient = new HttpClient();

    // Initialization state tracking
    private Task? _initializationTask;

    public AddSalePageViewModel(ForexClient client, IMapper mapper, INavigationService navigation)
    {
        this.client = client;
        this.mapper = mapper;
        this.navigation = navigation;
        CurrentSaleItem.PropertyChanged += SaleItemPropertyChanged;

        _initializationTask = LoadDataAsync();
    }

    [ObservableProperty] private DateTime date = DateTime.Now;
    [ObservableProperty] private decimal? totalAmount;
    [ObservableProperty] private decimal? finalAmount;
    [ObservableProperty] private decimal? totalAmountWithUserBalance;
    [ObservableProperty] private string note = string.Empty;

    [ObservableProperty] private SaleItemViewModel currentSaleItem = new();
    [ObservableProperty] private ObservableCollection<SaleItemViewModel> saleItems = [];
    [ObservableProperty] private SaleItemViewModel? selectedSaleItem = default;

    [ObservableProperty] private UserViewModel? customer;
    [ObservableProperty] private ObservableCollection<UserViewModel> availableCustomers = [];
    [ObservableProperty] private ObservableCollection<ProductViewModel> availableProducts = [];

    [ObservableProperty] private long editingSaleId = 0;

    #region Initialization

    private async Task EnsureInitializedAsync()
    {
        if (_initializationTask is not null)
        {
            await _initializationTask;
        }
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
            AvailableCustomers = mapper.Map<ObservableCollection<UserViewModel>>(response.Data!);
        else
            ErrorMessage = response.Message ?? "Mahsulot turlarini yuklashda xatolik.";
    }

    public async Task LoadProductsAsync()
    {
        FilteringRequest request = new()
        {
            Filters = new()
            {
                ["ProductType"] = ["include:Product"],
                ["Count"] = [">0"]
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
            return pr.ProductType;
        })
        .Where(pt => pt is not null && pt.Product is not null)
        .ToList();

        var grouped = allTypes.GroupBy(pt => pt.Product.Id);

        var products = new ObservableCollection<ProductViewModel>();

        foreach (var group in grouped)
        {
            var sampleType = group.First();
            var product = sampleType.Product;

            product.ProductTypes = new ObservableCollection<ProductTypeViewModel>(group);
            products.Add(product);
        }

        AvailableProducts = products;
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task Add()
    {
        if (CurrentSaleItem.Product is null ||
            CurrentSaleItem.BundleCount == null ||
            CurrentSaleItem.ProductType is null ||
            CurrentSaleItem.UnitPrice is null)
        {
            WarningMessage = "Mahsulot tanlanmagan yoki miqdor noto'g'ri!";
            return;
        }

        int needed = CurrentSaleItem.TotalCount ?? 0;

        bool isDuplicate = CurrentSaleItem.ProductType.Id > 0
            ? SaleItems.Any(s => s.ProductType?.Id == CurrentSaleItem.ProductType.Id)
            : SaleItems.Any(s => s.ProductType?.Type == CurrentSaleItem.ProductType.Type
                              && s.Product?.Id == CurrentSaleItem.Product?.Id);
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

            if (msgResult == MessageBoxResult.No)
                return;

            var window = new QuickProductEntryWindow(
                CurrentSaleItem.Product,
                CurrentSaleItem.ProductType,
                needed,
                currentStock,
                Date,
                client);

            if (window.ShowDialog() != true)
                return;

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
        SaleItems.Add(item);

        ClearCurrentSaleItem();
        RecalculateTotals();
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedSaleItem is null)
            return;

        bool hasCurrentData = CurrentSaleItem.Product is not null ||
                             CurrentSaleItem.BundleCount.HasValue;

        if (hasCurrentData)
        {
            var result = MessageBox.Show(
                "Hozirgi kiritilgan ma'lumotlar o'chib ketadi. Davom etmoqchimisiz?",
                "Ogohlantirish",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
                return;
        }

        CurrentSaleItem.PropertyChanged -= SaleItemPropertyChanged;

        try
        {
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
    private void DeleteItem(SaleItemViewModel item)
    {
        if (item is null)
            return;

        var result = MessageBox.Show(
            $"Mahsulotni o'chirishni tasdiqlaysizmi?",
            "O'chirishni tasdiqlash",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.No)
            return;

        item.PropertyChanged -= SaleItemPropertyChanged;
        SaleItems.Remove(item);
        RecalculateTotals();
    }

    [RelayCommand]
    private async Task Submit()
    {
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
            else ErrorMessage = response.Message ?? "Savdoni yangilashda xatolik!";
        }
        else
        {
            var response = await client.Sales.Create(request).Handle(isLoading => IsLoading = isLoading);

            if (isSuccess = response.IsSuccess)
            {
                SuccessMessage = $"Savdo muvaffaqiyatli yuborildi. Mahsulotlar soni: {SaleItems.Count}";

                // Mana shu yerda so'raydi va print funksiyasini chaqiradi
                var result = MessageBox.Show(
                    "Savdo muvaffaqiyatli saqlandi!\n\nChop etishni xohlaysizmi?",
                    "Muvaffaqiyat",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    await ShowPrintPreview();
            }
            else ErrorMessage = response.Message ?? "Savdoni ro'yxatga olishda xatolik!";
        }

        if (isSuccess)
        {
            Clear();
            navigation.GoBack();
        }
    }

    public async Task ShowPrintPreview()
    {
        if (SaleItems == null || !SaleItems.Any())
        {
            MessageBox.Show("Ko'rsatish uchun ma'lumot yo'q.", "Eslatma", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            // 1. Rasmlarni parallel yuklash (Tezlik siri)
            var uniqueUrls = SaleItems
                .Select(i => i.Product.DisplayImagePath)
                .Where(url => !string.IsNullOrEmpty(url) && !_imageCache.ContainsKey(url))
                .Distinct()
                .ToList();

            if (uniqueUrls.Any())
            {
                // Barcha rasmlarni bir vaqtda yuklashni boshlaymiz
                var tasks = uniqueUrls.Select(async url =>
                {
                    var bitmap = await DownloadBitmapAsync(url);
                    if (bitmap != null) _imageCache[url] = bitmap;
                });
                await Task.WhenAll(tasks);
            }

            // 2. Hujjatni yaratish
            var fixedDoc = CreateFixedDocumentForPrint();

            // 3. UI qismlari
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
            var shareButton = new Button
            {
                Content = "Telegram’da ulashish",
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
                Content = layout
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
        const double pageWidth = 793.7;
        const double pageHeight = 1122.5;
        const double margin = 25;
        const double footerSpace = 60; // Jami qatori + bet raqami uchun joy

        string[] headers = { "T/r", "Rasm", "Kod", "Nomi", "Razmer", "Qop soni", "Jami soni", "Narxi", "Jami summa" };
        double[] widths = { 35, 70, 60, 165, 60, 60, 70, 70, 143.7 };
        double tableWidth = pageWidth - margin * 2;

        var groupedItems = SaleItems
            .OrderBy(i => i.Product.Code)
            .GroupBy(i => i.Product.Code)
            .ToList();

        decimal grandTotalAmount = SaleItems.Sum(x => x.Amount ?? 0);
        double grandTotalBundle = SaleItems.Sum(x => (double)(x.BundleCount ?? 0));
        double grandTotalCount = SaleItems.Sum(x => (double)(x.TotalCount ?? 0));

        int currentGroupIndex = 0;
        int pageNumber = 1;
        int globalTr = 1;

        while (currentGroupIndex < groupedItems.Count)
        {
            var page = new FixedPage { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
            var container = new StackPanel { Margin = new Thickness(margin, 20, margin, 20) };

            // currentY — sahifada qancha joy ishlatilganini kuzatib boramiz
            double currentY = 40; // StackPanel top margin

            // Sarlavha faqat 1-betda
            if (pageNumber == 1)
            {
                var title = new TextBlock
                {
                    Text = "Sotilgan mahsulotlar ro'yxati",
                    FontSize = 20,
                    FontWeight = FontWeights.ExtraBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                title.Measure(new Size(tableWidth, double.PositiveInfinity));
                container.Children.Add(title);
                currentY += title.DesiredSize.Height + 5;

                var info = new TextBlock
                {
                    Text = $"Mijoz: {Customer?.Name.ToUpper() ?? "Naqd"} | Sana: {Date:dd.MM.yyyy}",
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                info.Measure(new Size(tableWidth, double.PositiveInfinity));
                container.Children.Add(info);
                currentY += info.DesiredSize.Height + 10;
            }

            var grid = new Grid { Width = tableWidth };
            foreach (var w in widths)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });

            // Header qatori
            AddRow(grid, true, 0, headers);
            // Header balandligini o'lchash
            {
                var hb = new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0.5),
                    Background = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
                    Padding = new Thickness(4, 5, 4, 5),
                    Child = new TextBlock { Text = "Jami soni", FontSize = 13, FontWeight = FontWeights.Bold }
                };
                hb.Measure(new Size(widths[6], double.PositiveInfinity));
                currentY += hb.DesiredSize.Height;
            }

            var groupsOnPage = new List<IGrouping<string, SaleItemViewModel>>();
            int tempGroupIndex = currentGroupIndex;

            // 0.5 sm = ~19px (96 DPI da 1sm = 37.8px, 0.5sm = 18.9px)
            const double safetyGap = 19.0;

            while (tempGroupIndex < groupedItems.Count)
            {
                var group = groupedItems[tempGroupIndex];

                // 1. Bir qator balandligini o'lchash
                double oneRowHeight = CalculateOneRowHeight(group.First(), widths);

                // 2. Guruhda nechta qator bor — shuncha ko'paytirish
                // (lekin rasm tufayli minimum 72px)
                int rowCount = group.Count();
                double groupHeight = Math.Max(oneRowHeight * rowCount, 72.0);

                // 3. Oxirgi guruh bo'lsa, Jami qatori uchun ham joy kerak
                bool isLastGroup = (tempGroupIndex == groupedItems.Count - 1);
                double jamiExtra = isLastGroup ? footerSpace : 0;

                // 4. Footer + safetyGap (0.5sm) + joriy ishlatilgan joy
                double usedSpace = currentY + groupHeight + jamiExtra + safetyGap;

                // 5. Sig'adimi?
                if (usedSpace > pageHeight - margin && tempGroupIndex > currentGroupIndex)
                    break; // Bu guruh keyingi betga

                groupsOnPage.Add(group);
                currentY += groupHeight;
                tempGroupIndex++;
            }

            // Jadvalni render qilish
            int gridRow = 1;
            foreach (var group in groupsOnPage)
            {
                int groupStartRow = gridRow;
                bool isFirstInGroup = true;

                foreach (var item in group)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    AddCellToGrid(grid, globalTr.ToString(), gridRow, 0, false, TextAlignment.Center);
                    AddCellToGrid(grid, item.Product.Code ?? "", gridRow, 2, false, TextAlignment.Left);
                    AddCellToGrid(grid, item.Product.Name ?? "", gridRow, 3, false, TextAlignment.Left);
                    AddCellToGrid(grid, item.ProductType.Type ?? "", gridRow, 4, false, TextAlignment.Center);
                    AddCellToGrid(grid, item.BundleCount?.ToString("N0") ?? "0", gridRow, 5, false, TextAlignment.Right);
                    AddCellToGrid(grid, item.TotalCount?.ToString("N0") ?? "0", gridRow, 6, false, TextAlignment.Right);
                    AddCellToGrid(grid, item.UnitPrice?.ToString("N2") ?? "0.00", gridRow, 7, false, TextAlignment.Right);
                    AddCellToGrid(grid, item.Amount?.ToString("N2") ?? "0.00", gridRow, 8, false, TextAlignment.Right);

                    if (isFirstInGroup)
                    {
                        var imgBorder = CreateImageCell(item.Product.DisplayImagePath);
                        Grid.SetRow(imgBorder, groupStartRow);
                        Grid.SetColumn(imgBorder, 1);
                        Grid.SetRowSpan(imgBorder, group.Count());
                        grid.Children.Add(imgBorder);
                        isFirstInGroup = false;
                    }

                    gridRow++;
                    globalTr++;
                }
            }

            // Jami qatori faqat oxirgi betda
            if (tempGroupIndex >= groupedItems.Count)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });

                var totalLabel = CreateCell("JAMI:", true, TextAlignment.Right);
                totalLabel.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                Grid.SetRow(totalLabel, gridRow);
                Grid.SetColumn(totalLabel, 0);
                Grid.SetColumnSpan(totalLabel, 5);
                grid.Children.Add(totalLabel);

                AddCellToGrid(grid, grandTotalBundle.ToString("N0"), gridRow, 5, true, TextAlignment.Right);
                AddCellToGrid(grid, grandTotalCount.ToString("N0"), gridRow, 6, true, TextAlignment.Right);

                var totalSumCell = CreateCell(grandTotalAmount.ToString("N2"), true, TextAlignment.Right);
                totalSumCell.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                Grid.SetRow(totalSumCell, gridRow);
                Grid.SetColumn(totalSumCell, 7);
                Grid.SetColumnSpan(totalSumCell, 2);
                grid.Children.Add(totalSumCell);
            }

            container.Children.Add(grid);
            page.Children.Add(container);

            var pnb = new TextBlock
            {
                Text = $"{pageNumber}-bet",
                FontSize = 10,
                Foreground = Brushes.Gray
            };
            FixedPage.SetRight(pnb, margin);
            FixedPage.SetBottom(pnb, 15);
            page.Children.Add(pnb);

            var pc = new PageContent();
            ((IAddChild)pc).AddChild(page);
            fixedDoc.Pages.Add(pc);

            currentGroupIndex = tempGroupIndex;
            pageNumber++;
        }

        return fixedDoc;
    }

    // Bir qatorning (bitta item) balandligini o'lchaydi
    private double CalculateOneRowHeight(SaleItemViewModel item, double[] widths)
    {
        double rowH = 0;
        var cells = new[]
        {
        (item.Product.Code     ?? "",        widths[2]),
        (item.Product.Name     ?? "",        widths[3]),
        (item.ProductType.Type ?? "",        widths[4]),
        (item.BundleCount?.ToString("N0") ?? "0",    widths[5]),
        (item.TotalCount?.ToString("N0")  ?? "0",    widths[6]),
        (item.UnitPrice?.ToString("N2")   ?? "0.00", widths[7]),
        (item.Amount?.ToString("N2")      ?? "0.00", widths[8]),
    };

        foreach (var (text, width) in cells)
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(4, 5, 4, 5),
                Child = new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap }
            };
            border.Measure(new Size(width, double.PositiveInfinity));
            if (border.DesiredSize.Height > rowH)
                rowH = border.DesiredSize.Height;
        }

        return rowH;
    }
    private async Task<BitmapSource> DownloadBitmapAsync(string url)
    {
        try
        {
            byte[] data = await _httpClient.GetByteArrayAsync(url);
            using (var ms = new MemoryStream(data))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.DecodePixelWidth = 150; // PDF sifati va hajmi uchun optimal
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }
        catch { return null; }
    }
    private Border CreateImageCell(string imagePath)
    {
        var border = new Border
        {
            Width = 70, // Kvadrat joy
            Height = 70,
            BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        if (!string.IsNullOrEmpty(imagePath) && _imageCache.TryGetValue(imagePath, out var bitmap))
        {
            var img = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform, // Seniorlar tanlovi: proporsiya buzilmaydi
                Margin = new Thickness(2)
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            border.Child = img;
        }
        else
        {
            border.Child = new TextBlock { Text = "Rasm yo'q", FontSize = 8, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
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
        TextAlignment[] alignments = {
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
    private void Clear()
    {
        SaleItems.Clear();
        Customer = null;
        TotalAmount = null;
        FinalAmount = null;
        Note = string.Empty;
        TotalAmountWithUserBalance = null;
        EditingSaleId = 0;
        ClearCurrentSaleItem();
    }
    private void ClearCurrentSaleItem()
    {
        CurrentSaleItem.PropertyChanged -= SaleItemPropertyChanged;
        CurrentSaleItem = new SaleItemViewModel();
        CurrentSaleItem.PropertyChanged += SaleItemPropertyChanged;
    }

    #endregion

    #region Property Changes

    partial void OnCustomerChanged(UserViewModel? value) => RecalculateTotalAmountWithUserBalance();

    private void SaleItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SaleItemViewModel.Amount))
        {
            RecalculateTotals();
        }
    }

    partial void OnFinalAmountChanged(decimal? value)
    {
        if (Customer is not null)
            TotalAmountWithUserBalance = Customer.Balance - FinalAmount;
    }

    partial void OnEditingSaleIdChanged(long value)
    {
        IsEditing = value > 0;
    }

    #endregion

    #region Private Helpers

    private void RecalculateTotals()
    {
        TotalAmount = SaleItems.Sum(x => x.Amount);
        FinalAmount = TotalAmount;
    }

    private void RecalculateTotalAmountWithUserBalance()
    {
        if (Customer is not null)
            TotalAmountWithUserBalance = Customer.Balance - TotalAmount;
    }

    #endregion

    #region Public Methods for External Use
    public async Task LoadSaleForEditAsync(long saleId, bool notifyOnLoad = true)
    {
        // Ma'lumotlar yuklanishini kutamiz
        await EnsureInitializedAsync();

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
            return;
        }

        var sale = mapper.Map<SaleViewModel>(response.Data.First());

        EditingSaleId = sale.Id;
        Date = sale.Date;
        Note = sale.Note ?? string.Empty;

        // Endi AvailableCustomers to'liq yuklangan
        var customer = AvailableCustomers.FirstOrDefault(c => c.Id == sale.CustomerId);
        if (customer is not null)
        {
            Customer = customer;
        }

        SaleItems.Clear();
        if (sale.SaleItems is not null)
        {
            foreach (var saleItem in sale.SaleItems)
            {
                var product = AvailableProducts.FirstOrDefault(p =>
                    p.Id == saleItem.ProductType?.Product?.Id);

                if (product == null && saleItem.ProductType?.Product is not null)
                {
                    product = mapper.Map<ProductViewModel>(saleItem.ProductType.Product);
                    product.ProductTypes = [];
                    AvailableProducts.Add(product);
                }

                ProductTypeViewModel? productType = null;
                if (product is not null && saleItem.ProductType is not null)
                {
                    productType = product.ProductTypes?.FirstOrDefault(pt =>
                        pt.Id == saleItem.ProductType.Id);

                    if (productType == null)
                    {
                        productType = mapper.Map<ProductTypeViewModel>(saleItem.ProductType);
                        product.ProductTypes ??= [];
                        product.ProductTypes.Add(productType);
                    }
                }

                var item = new SaleItemViewModel
                {
                    Product = product!,
                    ProductType = productType!,
                    BundleCount = saleItem.BundleCount,
                    UnitPrice = saleItem.UnitPrice,
                    Amount = saleItem.Amount,
                    TotalCount = saleItem.TotalCount
                };

                item.PropertyChanged += SaleItemPropertyChanged;
                SaleItems.Add(item);
            }
        }

        RecalculateTotals();
        if (notifyOnLoad)
            SuccessMessage = "Savdo tahrirlash uchun yuklandi.";
    }

    #endregion
}