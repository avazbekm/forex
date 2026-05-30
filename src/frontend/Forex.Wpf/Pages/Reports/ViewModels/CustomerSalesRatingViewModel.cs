namespace Forex.Wpf.Pages.Reports.ViewModels;

using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService;
using Forex.ClientService.Enums;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Commons;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Forex.Wpf.ViewModels;
using Forex.Wpf.Windows;
using System.Windows.Input;
using System.Diagnostics;

public partial class CustomerSalesRatingViewModel : ViewModelBase
{
    private readonly ForexClient _client;
    private readonly CommonReportDataService _commonData;

    [ObservableProperty] private ObservableCollection<CustomerSaleViewModel> customerSales = [];
    public ObservableCollection<UserViewModel> AvailableCustomers => _commonData.AvailableCustomers;

    [ObservableProperty] private UserViewModel? selectedCustomer;
    [ObservableProperty] private DateTime beginDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime endDate = DateTime.Today;
    
    public bool HasData => CustomerSales?.Count > 0;

    public CustomerSalesRatingViewModel(ForexClient client, CommonReportDataService commonData)
    {
        _client = client;
        _commonData = commonData;
    }

    partial void OnCustomerSalesChanged(ObservableCollection<CustomerSaleViewModel> value)
    {
        OnPropertyChanged(nameof(HasData));
    }

    [RelayCommand] 
    private async Task LoadSalesAsync()
    {
        IsLoading = true;

        try
        {
            var request = new FilteringRequest
            {
                Filters = new()
                {
                    ["date"] = [$">={BeginDate:o}", $"<{EndDate.AddDays(1):o}"],
                    ["customer"] = ["include"],
                    ["saleItems"] = ["include:productType.product"]
                }

            };
            // ... filter va request ...

            var response = await _client.Sales.Filter(request).Handle(l => IsLoading = l);
            if (!response.IsSuccess || response.Data == null)
            {
                ErrorMessage = "Savdolar yuklanmadi";
                CustomerSales = new ObservableCollection<CustomerSaleViewModel>(); // yangi collection
                return;
            }

            var tempList = new List<CustomerSaleViewModel>();
            int rowNumber = 1;

            foreach (var group in response.Data
                .Where(s => s.Customer != null)
                .GroupBy(s => s.Customer.Id))
            {
                var customer = group.First().Customer;

                if (SelectedCustomer != null && SelectedCustomer.Id != customer.Id)
                    continue;

                var vm = new CustomerSaleViewModel
                {
                    RowNumber = rowNumber++,
                    CustomerName = customer.Name ?? "Nomsiz mijoz"
                };

                foreach (var sale in group)
                {
                    if (sale.SaleItems == null) continue;

                    foreach (var item in sale.SaleItems)
                    {
                        if (item?.ProductType?.Product == null) continue;

                        var origin = item.ProductType.Product.ProductionOrigin;
                        int count = (int)item.TotalCount;

                        switch (origin)
                        {
                            case ProductionOrigin.Tayyor: vm.ReadyCount += count; break;
                            case ProductionOrigin.Aralash: vm.MixedCount += count; break;
                            case ProductionOrigin.Eva: vm.EvaCount += count; break;
                            default: vm.ReadyCount += count; break;
                        }
                    }
                }

                tempList.Add(vm);
            }

            // Eng muhimi — YANGI ObservableCollection yaratib beramiz!
            CustomerSales = new ObservableCollection<CustomerSaleViewModel>(tempList.OrderByDescending(x => x.TotalCount));

        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xatolik: {ex.Message}");
            CustomerSales = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearFilter()
    {
        SelectedCustomer = null;
        BeginDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        EndDate = DateTime.Today;
        await LoadSalesAsync();
    }

    [RelayCommand]
    private void Preview()
    {
        if (!CustomerSales.Any())
        {
            MessageBox.Show("Ko'rsatish uchun ma'lumot yo'q!", "Xabar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var doc = CreateFixedDocument();
        var viewer = new DocumentViewer { Document = doc, Margin = new Thickness(20) };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };

        // SAQLASH
        var saveButton = new Button
        {
            Content = "Saqlash",
            Margin = new Thickness(0,0,5,0),
            Padding = new Thickness(15, 8, 15, 8),
            Background = Brushes.Gray, Foreground = Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0)
        };
        saveButton.Click += (s, e) =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = $"MijozlarReytingi_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.pdf" };
            if (dlg.ShowDialog() == true)
            {
                SaveFixedDocumentToPdf(doc, dlg.FileName);
                MessageBox.Show("Saqlandi!");
            }
        };
        toolbar.Children.Add(saveButton);

        // OCHISH
        var openButton = new Button
        {
            Content = "Ochish",
            Margin = new Thickness(0,0,5,0),
            Padding = new Thickness(15, 8, 15, 8),
            Background = Brushes.Gray, Foreground = Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0)
        };
        openButton.Click += (s, e) =>
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string folder = Path.Combine(docs, "Forex", "Hisobotlar");
            Directory.CreateDirectory(folder);
            string fileName = $"MijozlarReytingi_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.pdf";
            string path = Path.Combine(folder, fileName);
            SaveFixedDocumentToPdf(doc, path);
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch {}
        };
        toolbar.Children.Add(openButton);

        // ULASHISH
        var shareButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.ShareVariant, Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "Ulashish", Margin = new Thickness(8,0,0,0), VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Padding = new Thickness(15, 8, 15, 8),
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
                string folder = Path.Combine(docs, "Forex", "Hisobotlar");
                Directory.CreateDirectory(folder);

                string fileName = $"MijozlarReytingi_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.pdf";
                string path = Path.Combine(folder, fileName);

                SaveFixedDocumentToPdf(doc, path);

                if (File.Exists(path))
                {
                    var window = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);
                    var viewModel = App.AppHost!.Services.GetRequiredService<TelegramShareViewModel>();
                    viewModel.PdfFilePath = path;
                    viewModel.MessageCaption = $"Mijozlar savdo reytingi\nDavr: {BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}";

                    var shareWindow = new TelegramShareWindow
                    {
                        DataContext = viewModel,
                        Owner = window ?? Application.Current.MainWindow,
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

        toolbar.Children.Add(shareButton);

        var layout = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        layout.Children.Add(toolbar);
        layout.Children.Add(viewer);

        var window = new Window
        {
            Title = "Mijozlar bo‘yicha savdo reytingi",
            Width = 1000,
            Height = 800,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = layout,
            Owner = Application.Current.MainWindow,
            ShowInTaskbar = false
        };

        window.ShowDialog();
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

    [RelayCommand]
    private void Print()
    {
        if (!CustomerSales.Any())
        {
            MessageBox.Show("Chop etish uchun ma’lumot yo‘q!", "Xabar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new PrintDialog();
        if (dlg.ShowDialog() == true)
        {
            dlg.PrintDocument(CreateFixedDocument().DocumentPaginator, "Mijozlar bo‘yicha savdo reytingi");
        }
    }

    [RelayCommand]
    private void ExportToExcel()
    {
        if (!CustomerSales.Any())
        {
            MessageBox.Show("Excelga eksport qilish uchun ma'lumot yo‘q!", "Eslatma", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel fayllari (*.xlsx)|*.xlsx",
            FileName = $"MijozlarReytingi_{DateTime.Today:dd.MM.yyyy}.xlsx"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Savdo reytingi");

            int row = 1;

            // Title
            ws.Cell(row, 1).Value = "MIJOZLAR BO‘YICHA SAVDO REYTINGI";
            ws.Range(row, 1, row, 6).Merge().Style
                .Font.SetBold().Font.SetFontSize(16)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row += 2;

            // Date
            ws.Cell(row, 1).Value = $"Sana: {DateTime.Today:dd.MM.yyyy}";
            ws.Range(row, 1, row, 6).Merge().Style.Font.SetFontSize(12);
            row += 2;

            // Header
            string[] headers = { "T/r", "Mijoz nomi", "Tayyor", "Aralash", "Eva", "Jami" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(row, i + 1).Value = headers[i];

            ws.Range(row, 1, row, headers.Length).Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.LightGray);
            row++;

            // Data
            foreach (var x in CustomerSales)
            {
                ws.Cell(row, 1).Value = x.RowNumber;
                ws.Cell(row, 2).Value = x.CustomerName;
                ws.Cell(row, 3).Value = x.ReadyCount;
                ws.Cell(row, 4).Value = x.MixedCount;
                ws.Cell(row, 5).Value = x.EvaCount;
                ws.Cell(row, 6).Value = x.TotalCount;
                row++;
            }

            // Total row
            ws.Cell(row, 5).Value = "JAMI:";
            ws.Cell(row, 6).Value = CustomerSales.Sum(i => i.TotalCount);
            ws.Range(row, 5, row, 6).Style.Font.SetBold();

            ws.Columns().AdjustToContents();
            workbook.SaveAs(dialog.FileName);

            MessageBox.Show("Excel fayl muvaffaqiyatli saqlandi!", "Tayyor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xatolik: {ex.Message}");
        }
    }



    // 🔵 A4 PDF/Print hujjat
    private FixedDocument CreateFixedDocument()
    {
        var doc = new FixedDocument();

        double pageWidth = 793.7;
        double pageHeight = 1122.5;

        double marginTop = 40;
        double marginBottom = 40;
        double marginLeft = 30;
        double marginRight = 30;

        double titleHeight = 40;
        double dateHeight = 30;
        double rowHeight = 25;

        // Ma'lumotlarni saralangan holda olamiz
        var items = CustomerSales.ToList();
        double total = items.Sum(x => x.TotalCount);

        double tableAvailableHeight =
            pageHeight - marginTop - marginBottom - titleHeight - dateHeight - 30; // 30 footer uchun joy

        int rowsPerPage = (int)(tableAvailableHeight / rowHeight);
        if (rowsPerPage < 1) rowsPerPage = 1;

        int totalPages = (int)Math.Ceiling(items.Count / (double)rowsPerPage);
        if (totalPages == 0) totalPages = 1;

        for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
        {
            var page = new FixedPage
            {
                Width = pageWidth,
                Height = pageHeight,
                Background = Brushes.White
            };

            // Asosiy konteyner sifatida Grid ishlatamiz (footer pastda turishi uchun)
            var mainGrid = new Grid
            {
                Width = pageWidth - marginLeft - marginRight,
                Height = pageHeight - marginTop - marginBottom,
                Margin = new Thickness(marginLeft, marginTop, marginRight, marginBottom)
            };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Kontent
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer uchun

            var container = new StackPanel();

            // Title
            container.Children.Add(new TextBlock
            {
                Text = "Mijozlar bo‘yicha savdo reytingi",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            });

            // Davr oralig'i (Sen aytgan o'zgarish)
            container.Children.Add(new TextBlock
            {
                Text = $"Davr oralig'i: {BeginDate:dd.MM.yyyy} dan {EndDate:dd.MM.yyyy} gacha",
                FontSize = 14,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Table
            var table = new Grid();
            double[] widths = { 50, 250, 100, 100, 100, 120 };
            foreach (var w in widths)
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });

            AddRow(table, true, "T/r", "Mijoz", "Tayyor", "Aralash", "Eva", "Jami");

            int start = pageIndex * rowsPerPage;
            int count = Math.Min(rowsPerPage, items.Count - start);

            for (int i = 0; i < count; i++)
            {
                var x = items[start + i];

                AddRow(table, false,
                    (start + i + 1).ToString(), // ⭐ T/R 1 dan boshlab tartiblandi
                    x.CustomerName,
                    x.ReadyCount.ToString("N0"),
                    x.MixedCount.ToString("N0"),
                    x.EvaCount.ToString("N0"),
                    x.TotalCount.ToString("N0"));
            }

            // Oxirgi betda JAMI qatori
            if (pageIndex == totalPages - 1)
            {
                AddRow(table, true, "", "JAMI:", "", "", "", total.ToString("N0"));
            }

            container.Children.Add(table);
            Grid.SetRow(container, 0);
            mainGrid.Children.Add(container);

            // ⭐ Sahifa raqami (O'ng taraf eng past qismda)
            var footerPageNum = new TextBlock
            {
                Text = $"{pageIndex + 1} - bet / {totalPages}",
                FontSize = 11,
                Foreground = Brushes.DarkGray,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(footerPageNum, 1);
            mainGrid.Children.Add(footerPageNum);

            page.Children.Add(mainGrid);

            var pc = new PageContent();
            ((IAddChild)pc).AddChild(page);
            doc.Pages.Add(pc);
        }

        return doc;
    }
    private void AddRow(Grid grid, bool isHeader, params string[] values)
    {
        int row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < values.Length; i++)
        {
            TextAlignment align =
                isHeader ? TextAlignment.Center : i switch
                {
                    0 => TextAlignment.Right,
                    1 => TextAlignment.Left,
                    2 => TextAlignment.Center,
                    3 => TextAlignment.Center,
                    4 => TextAlignment.Center,
                    5 => TextAlignment.Right,
                    _ => TextAlignment.Center
                };

            var tb = new TextBlock
            {
                Text = values[i],
                Padding = new Thickness(4, 5, 4, 5),
                FontSize = isHeader ? 12 : 11,
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                TextAlignment = align,
                VerticalAlignment = VerticalAlignment.Center
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

}