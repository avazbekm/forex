namespace Forex.Wpf.Pages.Reports.ViewModels;

using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Commons;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Forex.Wpf.ViewModels;
using Forex.Wpf.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public partial class SalesHistoryReportViewModel : ViewModelBase
{
    private readonly ForexClient client;
    private readonly CommonReportDataService commonData;
    private readonly ObservableCollection<SaleHistoryItemViewModel> allItems = [];

    [ObservableProperty]
    private ObservableCollection<SaleHistoryItemViewModel> filteredItems = [];

    [ObservableProperty]
    private decimal totalSalesAmount;

    public ObservableCollection<UserViewModel> AvailableCustomers => commonData.AvailableCustomers;
    public ObservableCollection<ProductViewModel> AvailableProducts => commonData.AvailableProducts;

    [ObservableProperty] private UserViewModel? selectedCustomer;
    [ObservableProperty] private ProductViewModel? selectedProduct;
    [ObservableProperty] private ProductViewModel? selectedCode;
    [ObservableProperty] private DateTime beginDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime endDate = DateTime.Today;
    
    partial void OnSelectedCustomerChanged(UserViewModel? value) => _ = LoadAsync();
    partial void OnBeginDateChanged(DateTime value) => _ = LoadAsync();
    partial void OnEndDateChanged(DateTime value) => _ = LoadAsync();
    
    public bool HasData => FilteredItems?.Count > 0;

    public SalesHistoryReportViewModel(ForexClient client, CommonReportDataService commonData)
    {
        this.client = client;
        this.commonData = commonData;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SelectedCustomer) or nameof(SelectedProduct) or nameof(SelectedCode))
                ApplyFilters();
            if (e.PropertyName is nameof(FilteredItems))
                OnPropertyChanged(nameof(HasData));
        };
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        allItems.Clear();
        var request = new FilteringRequest
        {
            Filters = new()
            {
                ["date"] = [$">={BeginDate:o}", $"<{EndDate.AddDays(1):o}"],
                ["customer"] = ["include"],
                ["saleItems"] = ["include:productType.product.unitMeasure"]
            },
            Descending = true,
            SortBy = "date"
        };

        var response = await client.Sales.Filter(request).Handle(l => IsLoading = l);
        if (!response.IsSuccess)
        {
            ErrorMessage = "Sotuvlar yuklanmadi";
            return;
        }

        foreach (var sale in response.Data)
        {
            if (sale.SaleItems == null) continue;
            foreach (var item in sale.SaleItems)
            {
                var product = item.ProductType?.Product;
                if (product == null) continue;
                allItems.Add(new SaleHistoryItemViewModel
                {
                    Date = sale.Date.ToLocalTime(),
                    Customer = sale.Customer?.Name ?? "-",
                    Code = product.Code ?? "-",
                    ProductName = product.Name ?? "-",
                    Type = item.ProductType?.Type ?? "-",
                    BundleCount = item.BundleCount,
                    BundleItemCount = item.ProductType?.BundleItemCount ?? 0,
                    TotalCount = item.TotalCount,
                    UnitMeasure = product.UnitMeasure?.Name ?? "dona",
                    UnitPrice = item.UnitPrice,
                    Amount = item.Amount
                });
            }
        }
        ApplyFilters();
    }

    [RelayCommand]
    private void ClearFilter()
    {
        SelectedCustomer = null;
        SelectedProduct = null;
        SelectedCode = null;
        BeginDate = DateTime.Today;
        EndDate = DateTime.Today;
    }

    [RelayCommand]
    private async Task Filter() => await LoadAsync();

        [RelayCommand]
    private void Preview()
    {
        if (!FilteredItems.Any())
        {
            InfoMessage = "Ko'rsatish uchun ma'lumot yo'q.";
            return;
        }

        var doc = CreateFixedDocument();
        var viewer = new DocumentViewer { Document = doc, Margin = new Thickness(20) };
        var toolbar = new StackPanel { 
            Orientation = Orientation.Horizontal, 
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10, 8, 10, 8)
        };

        string? savedPdfPath = null;

        // Saqlash tugmasi
        var saveButton = new Button
        {
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(4),
            Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        var saveContent = new StackPanel { Orientation = Orientation.Horizontal };
        saveContent.Children.Add(new TextBlock { Text = "💾", FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        saveContent.Children.Add(new TextBlock { Text = " Saqlash", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        saveButton.Content = saveContent;

        saveButton.Click += (s, e) =>
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF fayllar (*.pdf)|*.pdf",
                    FileName = $"Savdo_tarixi_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.pdf",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };
                if (saveDialog.ShowDialog() == true)
                {
                    SaveFixedDocumentToPdf(doc, saveDialog.FileName, 96);
                    savedPdfPath = saveDialog.FileName;
                    MessageBox.Show("Fayl muvaffaqiyatli saqlandi!", "Saqlandi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex) { MessageBox.Show($"Saqlashda xatolik: {ex.Message}"); }
        };

        // Ochish tugmasi
        var openButton = new Button
        {
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(4),
            Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        var openContent = new StackPanel { Orientation = Orientation.Horizontal };
        openContent.Children.Add(new TextBlock { Text = "📂", FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        openContent.Children.Add(new TextBlock { Text = " Ochish", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        openButton.Content = openContent;

        openButton.Click += (s, e) =>
        {
            try
            {
                if (string.IsNullOrEmpty(savedPdfPath) || !File.Exists(savedPdfPath))
                {
                    string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string folder = Path.Combine(docs, "Forex");
                    Directory.CreateDirectory(folder);
                    savedPdfPath = Path.Combine(folder, $"Savdo_tarixi_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.pdf");
                    SaveFixedDocumentToPdf(doc, savedPdfPath, 96);
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(savedPdfPath) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show($"Ochishda xatolik: {ex.Message}"); }
        };

        // Ulashish tugmasi
        var shareButton = new Button
        {
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(4),
            Background = new SolidColorBrush(Color.FromRgb(0, 136, 204)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        var shareContent = new StackPanel { Orientation = Orientation.Horizontal };
        shareContent.Children.Add(new TextBlock { Text = "📤", FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        shareContent.Children.Add(new TextBlock { Text = " Ulashish", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        shareButton.Content = shareContent;

        shareButton.Click += (s, e) =>
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string folder = Path.Combine(docs, "Forex");
                Directory.CreateDirectory(folder);

                string fileName = $"Savdo_tarixi_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.pdf";
                string path = Path.Combine(folder, fileName);

                SaveFixedDocumentToPdf(doc, path, 96);

                if (File.Exists(path))
                {
                    var activeWin = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);
                    var viewModel = App.AppHost!.Services.GetRequiredService<TelegramShareViewModel>();
                    viewModel.PdfFilePath = path;
                    viewModel.MessageCaption = $"Savdo tarixi\nDavr: {BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}";

                    var shareWindow = new TelegramShareWindow
                    {
                        DataContext = viewModel,
                        Owner = activeWin ?? Application.Current.MainWindow,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };

                    shareWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ulashishda xatolik: {ex.Message}");
            }
        };

        toolbar.Children.Add(saveButton);
        toolbar.Children.Add(openButton);
        toolbar.Children.Add(shareButton);

        var layout = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        layout.Children.Add(toolbar);
        layout.Children.Add(viewer);

        var window = new Window
        {
            Title = $"Savdo tarixi * {BeginDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}",
            Width = 1000,
            Height = 800,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = layout,
            Owner = Application.Current.MainWindow,
            ShowInTaskbar = false
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void Print()
    {
        if (!FilteredItems.Any())
        {
            InfoMessage = "Chop etish uchun ma'lumot yo'q.";
            return;
        }
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() == true)
            dlg.PrintDocument(CreateFixedDocument().DocumentPaginator, "Savdo tarixi");
    }

    [RelayCommand]
    private async Task ExportToExcel()
    {
        if (!FilteredItems.Any())
        {
            MessageBox.Show("Excelga eksport qilish uchun ma'lumot yo'q.", "Eslatma", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel fayllari (*.xlsx)|*.xlsx",
            FileName = $"Savdo_tarixi_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.xlsx"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Savdo tarixi");

            int row = 1;
            ws.Cell(row, 1).Value = "SAVDO TARIXI HISOBOTI";
            ws.Range(row, 1, row, 11).Merge().Style
                .Font.SetBold().Font.SetFontSize(18)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row += 2;

            ws.Cell(row, 1).Value = $"Davr: {BeginDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}";
            ws.Range(row, 1, row, 11).Merge().Style.Font.SetBold().Font.SetFontSize(14);
            row += 2;

            string[] headers = { "Sana", "Mijoz", "Kodi", "Nomi", "Razmer", "Qop soni", "Donasi", "Jami", "O'lchov", "Narxi", "Umumiy summa" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(row, i + 1).Value = headers[i];
            ws.Range(row, 1, row, 11).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
            row++;

            foreach (var item in FilteredItems)
            {
                ws.Cell(row, 1).Value = item.Date.ToString("dd.MM.yyyy");
                ws.Cell(row, 2).Value = item.Customer;
                ws.Cell(row, 3).Value = item.Code;
                ws.Cell(row, 4).Value = item.ProductName;
                ws.Cell(row, 5).Value = item.Type;
                ws.Cell(row, 6).Value = item.BundleCount;
                ws.Cell(row, 7).Value = item.BundleItemCount;
                ws.Cell(row, 8).Value = item.TotalCount;
                ws.Cell(row, 9).Value = item.UnitMeasure;
                ws.Cell(row, 10).Value = item.UnitPrice;
                ws.Cell(row, 11).Value = item.Amount;
                row++;
            }

            var totalAmount = FilteredItems.Sum(x => x.Amount);
            ws.Cell(row, 1).Value = "JAMI:";
            ws.Cell(row, 1).Style.Font.SetBold();
            ws.Cell(row, 11).Value = totalAmount;
            ws.Cell(row, 11).Style.Font.SetBold().NumberFormat.Format = "#,##0.00";
            ws.Columns().AdjustToContents();
            workbook.SaveAs(dialog.FileName);
            MessageBox.Show("Excel fayl muvaffaqiyatli saqlandi!", "Tayyor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xatolik: {ex.Message}");
        }
    }

    private void ApplyFilters()
    {
        var result = allItems.AsEnumerable();
        if (SelectedCustomer != null)
            result = result.Where(x => x.Customer == SelectedCustomer.Name);
        if (SelectedProduct != null)
            result = result.Where(x => x.ProductName == SelectedProduct.Name);
        if (SelectedCode != null)
            result = result.Where(x => x.Code == SelectedCode.Code);
        FilteredItems = new ObservableCollection<SaleHistoryItemViewModel>(result);
        TotalSalesAmount = FilteredItems.Sum(x => x.Amount);
    }

    private FixedDocument CreateFixedDocument()
    {
        var doc = new FixedDocument();
        const double pageWidth = 794;
        const double pageHeight = 1123;
        const double marginHorizontal = 45;
        const double marginVertical = 25;
        const double contentWidth = pageWidth - (2 * marginHorizontal);
        const double contentHeight = pageHeight - (2 * marginVertical);
        const double reservedSpaceAtBottom = 80;

        var allItemsList = FilteredItems.ToList();
        int processedItems = 0;
        int pageIndex = 0;

        while (processedItems < allItemsList.Count)
        {
            var page = new FixedPage { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
            var container = new Grid { Width = contentWidth, Margin = new Thickness(marginHorizontal, marginVertical, marginHorizontal, marginVertical) };
            var stack = new StackPanel();

            if (pageIndex == 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "SAVDO TARIXI HISOBOTI",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8),
                    Foreground = Brushes.DarkBlue
                });
                stack.Children.Add(new TextBlock
                {
                    Text = string.Format("Davr: {0:dd.MM.yyyy} - {1:dd.MM.yyyy}", BeginDate, EndDate),
                    FontSize = 15,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 25)
                });
            }

            var table = new Grid { Width = contentWidth };
            double[] widths = { 56, 80, 52, 60, 58, 60, 60, 52, 50, 70, 100 };
            foreach (var w in widths)
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });

            AddRow(table, true, "Sana", "Mijoz", "Kodi", "Nomi", "Razmer", "Qop soni", "Donasi", "Jami", "O'lchov", "Narxi", "Umumiy summa");

            while (processedItems < allItemsList.Count)
            {
                var item = allItemsList[processedItems];
                stack.Children.Add(table);
                stack.Measure(new Size(contentWidth, double.PositiveInfinity));
                double currentTableHeight = stack.DesiredSize.Height;
                stack.Children.Remove(table);

                double limit = contentHeight - (processedItems == allItemsList.Count - 1 ? reservedSpaceAtBottom : 30);
                if (currentTableHeight + 30 > limit)
                    break;

                AddRow(table, false,
                    item.Date.ToString("dd.MM.yyyy"),
                    item.Customer ?? "",
                    item.Code ?? "",
                    item.ProductName ?? "",
                    item.Type ?? "",
                    item.BundleCount.ToString("N0"),
                    item.BundleItemCount.ToString("N0"),
                    item.TotalCount.ToString("N0"),
                    item.UnitMeasure ?? "",
                    item.UnitPrice.ToString("N2"),
                    item.Amount.ToString("N2"));
                processedItems++;
            }

            if (processedItems >= allItemsList.Count)
            {
                var totalBundleCount = allItemsList.Sum(x => x.BundleCount);
                var totalTotalCount = allItemsList.Sum(x => x.TotalCount);
                var totalAmount = allItemsList.Sum(x => x.Amount);
                AddRow(table, true, "JAMI:", "", "", "", "",
                    totalBundleCount.ToString("N0"), "",
                    totalTotalCount.ToString("N0"), "", "",
                    totalAmount.ToString("N2"));
            }

            stack.Children.Add(table);
            container.Children.Add(stack);
            page.Children.Add(container);
            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(page);
            doc.Pages.Add(pageContent);
            pageIndex++;
        }
        return doc;
    }

    private void AddRow(Grid grid, bool isHeader, params string[] cells)
    {
        int row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < cells.Length; i++)
        {
            TextAlignment alignment;
            if (isHeader)
                alignment = TextAlignment.Center;
            else if (i == 1 || i == 3)
                alignment = TextAlignment.Left;
            else if (i == 9 || i == 10)
                alignment = TextAlignment.Right;
            else
                alignment = TextAlignment.Center;

            var tb = new TextBlock
            {
                Text = cells[i],
                Padding = new Thickness(4, 5, 4, 5),
                FontSize = isHeader ? 11 : 10.5,
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Medium,
                TextAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.WrapWithOverflow
            };

            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0.5),
                Background = isHeader ? Brushes.LightGray : Brushes.Transparent,
                Child = tb
            };

            Grid.SetRow(border, row);
            Grid.SetColumn(border, i);
            grid.Children.Add(border);
        }
    }

    private void SaveFixedDocumentToPdf(FixedDocument doc, string path, int dpi = 600)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);

            using var pdfDoc = new PdfSharp.Pdf.PdfDocument();

            foreach (var pageContent in doc.Pages)
            {
                var fixedPage = pageContent.GetPageRoot(false);
                if (fixedPage is null) continue;

                fixedPage.Measure(new Size(fixedPage.Width, fixedPage.Height));
                fixedPage.Arrange(new Rect(0, 0, fixedPage.Width, fixedPage.Height));
                fixedPage.UpdateLayout();

                double scale = dpi / 96.0;

                var bitmap = new RenderTargetBitmap(
                    (int)(fixedPage.Width * scale),
                    (int)(fixedPage.Height * scale),
                    dpi, dpi,
                    PixelFormats.Pbgra32);

                bitmap.Render(fixedPage);

                var encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using var stream = new MemoryStream();
                encoder.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);

                using var xImage = XImage.FromStream(stream);
                var pdfPage = pdfDoc.AddPage();
                pdfPage.Width = XUnit.FromPoint(fixedPage.Width);
                pdfPage.Height = XUnit.FromPoint(fixedPage.Height);

                using var gfx = XGraphics.FromPdfPage(pdfPage);
                gfx.DrawImage(xImage, 0, 0, fixedPage.Width, fixedPage.Height);
            }

            pdfDoc.Save(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PDF saqlashda xatolik: {ex.Message}");
        }
    }
}
