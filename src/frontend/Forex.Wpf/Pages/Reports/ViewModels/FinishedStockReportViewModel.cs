namespace Forex.Wpf.Pages.Reports.ViewModels;

using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Commons;
using Forex.Wpf.Common.Services;
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
using Forex.Wpf.Windows;
using System.Windows.Input;

public partial class FinishedStockReportViewModel : PagedReportViewModel<FinishedStockItemViewModel>
{
    private readonly ForexClient _client;
    private readonly CommonReportDataService _commonData;

    private readonly ObservableCollection<FinishedStockItemViewModel> _allItems = [];

    // UI ga ko'rinadigan filtrlangan ro'yxat
    [ObservableProperty]
    private ObservableCollection<FinishedStockItemViewModel> items = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalAmount))]
    private string? searchText;

    [ObservableProperty]
    private decimal totalAmount;

    [ObservableProperty]
    private int summaryQty;

    public bool HasData => Items?.Count > 0;

    public FinishedStockReportViewModel(ForexClient client, CommonReportDataService commonData)
    {
        _client = client;
        _commonData = commonData;

        this.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SearchText))
                ApplyFilters();
            if (e.PropertyName is nameof(Items))
                OnPropertyChanged(nameof(HasData));
        };
    }


    #region Commands

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        _allItems.Clear();
        Items.Clear();

        try
        {
            var request = new FilteringRequest
            {
                Filters = new()
                {
                    ["productType"] = ["include:product.unitMeasure"],
                    ["ProductEntries"] = ["include"],
                    ["count"] = [">0"]
                }
            };

            var response = await _client.ProductResidues.Filter(request).Handle(l => IsLoading = l);

            if (!response.IsSuccess)
            {
                ErrorMessage = "Tayyor mahsulot qoldiqlari yuklanmadi";
                return;
            }

            foreach (var stock in response.Data)
            {
                var pt = stock.ProductType;
                if (pt is null) continue;

                var product = pt.Product;
                if (product is null) continue;

                decimal unitPrice = stock.ProductType.UnitPrice;

                _allItems.Add(new FinishedStockItemViewModel
                {
                    Code = product.Code ?? "-",
                    Name = product.Name ?? "-",
                    Type = pt.Type ?? "-",
                    BundleItemCount = pt.BundleItemCount,
                    TotalCount = stock.Count,
                    UnitPrice = unitPrice,
                    TotalAmount = unitPrice * stock.Count
                });
            }

            ApplyFilters();
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void Print()
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() == true)
        {
            dlg.PrintDocument(CreateFixedDocument().DocumentPaginator, "Tayyor mahsulot qoldig'i");
        }
    }

    [RelayCommand]
    private void ExportToExcel()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel fayllari (*.xlsx)|*.xlsx",
            FileName = $"TayyorMahsulot_{DateTime.Today:dd.MM.yyyy}.xlsx"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Tayyor mahsulot qoldig'i");

            int row = 1;

            ws.Cell(row, 1).Value = "TAYYOR MAHSULOT QOLDIG'I";
            ws.Range(row, 1, row, 8).Merge().Style
                .Font.SetBold().Font.SetFontSize(16)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row += 2;

            ws.Cell(row, 1).Value = $"Sana: {DateTime.Today:dd.MM.yyyy}";
            ws.Range(row, 1, row, 8).Merge().Style.Font.SetFontSize(12);
            row += 2;

            // Header
            string[] headers = { "Kodi", "Nomi", "Razmer", "Donasi", "Qop soni", "Jami", "Narxi", "Umumiy" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(row, i + 1).Value = headers[i];

            ws.Range(row, 1, row, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
            row++;

            // Data
            foreach (var x in Items)
            {
                ws.Cell(row, 1).Value = x.Code;
                ws.Cell(row, 2).Value = x.Name;
                ws.Cell(row, 3).Value = x.Type;
                ws.Cell(row, 4).Value = x.BundleItemCount; // Qopdagi
                ws.Cell(row, 5).Value = x.BundleCount;     // Qop soni
                ws.Cell(row, 6).Value = x.TotalCount;     // Jami
                ws.Cell(row, 7).Value = x.UnitPrice;      // Narxi
                ws.Cell(row, 8).Value = x.TotalAmount;    // Umumiy
                row++;
            }

            // Total summa
            var totalAmount = Items.Sum(i => i.TotalAmount);
            ws.Cell(row, 7).Value = "JAMI:";
            ws.Cell(row, 7).Style.Font.SetBold();
            ws.Cell(row, 8).Value = totalAmount;
            ws.Cell(row, 8).Style.Font.SetBold().NumberFormat.Format = "#,##0.00";

            ws.Columns().AdjustToContents();
            workbook.SaveAs(dialog.FileName);

            MessageBox.Show("Excel fayl muvaffaqiyatli saqlandi!", "Tayyor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xatolik: {ex.Message}");
        }
    }

        [RelayCommand]
    private void Preview()
    {
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
                    FileName = $"TayyorMahsulot_{DateTime.Today:dd.MM.yyyy}.pdf",
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
                    savedPdfPath = Path.Combine(folder, $"TayyorMahsulot_{DateTime.Today:dd.MM.yyyy}.pdf");
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

                string fileName = $"TayyorMahsulot_{DateTime.Today:dd.MM.yyyy}.pdf";
                string path = Path.Combine(folder, fileName);

                SaveFixedDocumentToPdf(doc, path, 96);

                if (File.Exists(path))
                {
                    var activeWin = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);
                    var viewModel = App.AppHost!.Services.GetRequiredService<TelegramShareViewModel>();
                    viewModel.PdfFilePath = path;
                    viewModel.MessageCaption = $"Tayyor mahsulot qoldig'i\nSana: {DateTime.Today:dd.MM.yyyy}";

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
            Title = "Tayyor mahsulot qoldig'i",
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
    private void ClearFilter()
    {
        SearchText = null;
    }

    #endregion Commands

    private void ApplyFilters()
    {
        var result = _allItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            result = result.Where(x =>
                TransliterationHelper.ContainsIgnoreScript(x.Code ?? "", SearchText) ||
                TransliterationHelper.ContainsIgnoreScript(x.Name ?? "", SearchText));
        }

        Items = new ObservableCollection<FinishedStockItemViewModel>(result);
        SetSource(Items);
        TotalAmount = Items.Sum(x => x.TotalAmount);
        SummaryQty = Items.Sum(x => x.TotalCount);
    }

    // PDF/Print uchun document yaratish (PASTDAN 25mm BO'SH JOY!)
    private FixedDocument CreateFixedDocument()
    {
        var doc = new FixedDocument();
        double pageWidth = 793.7;
        double pageHeight = 1122.5;

        double marginTop = 38, marginBottom = 30, marginLeft = 30, marginRight = 30;
        double titleHeight = 40, dateHeight = 30, rowHeight = 25;

        var items = Items.ToList();
        var totalSum = items.Sum(i => i.TotalAmount);

        List<List<int>> pagesMapping = new List<List<int>>();
        int currentItemIndex = 0;
        int currentPage = 0;

        // --- SAHIFALARNI HISOB-KITOBI ---
        while (currentItemIndex < items.Count)
        {
            double availableHeight;

            if (currentPage == 0)
            {
                // 1-betda sarlavha bor. 
                // Pastda 1 qator joy qolishi uchun marginBottom va rowHeight (25px) ni hisobga olamiz
                availableHeight = pageHeight - marginTop - marginBottom - titleHeight - dateHeight - rowHeight - 20;
            }
            else
            {
                // 2, 3, 4... betlarda sarlavha yo'q.
                // Jadval oxiri va footer orasida 1 qator (25px) qolishi uchun rowHeight ayiramiz
                availableHeight = pageHeight - marginTop - marginBottom - rowHeight - 20;
            }

            int rowsThisPage = (int)(availableHeight / rowHeight) - 1; // -1 Header uchun

            // Agar bu oxirgi elementlar bo'lmasa, JAMI qatori uchun joy tashlanadi
            if (currentItemIndex + rowsThisPage < items.Count)
            {
                rowsThisPage -= 1;
            }

            if (rowsThisPage < 1) rowsThisPage = 1;

            var pageItems = new List<int>();
            for (int i = 0; i < rowsThisPage && currentItemIndex < items.Count; i++)
            {
                pageItems.Add(currentItemIndex);
                currentItemIndex++;
            }
            pagesMapping.Add(pageItems);
            currentPage++;
        }

        int totalPages = pagesMapping.Count;

        for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
        {
            var page = new FixedPage { Width = pageWidth, Height = pageHeight, Background = Brushes.White };

            // Asosiy Grid - Footer pastda qat'iy turishi uchun Height berilgan
            var mainGrid = new Grid
            {
                Width = pageWidth - marginLeft - marginRight,
                Height = pageHeight - marginTop - marginBottom,
                Margin = new Thickness(marginLeft, marginTop, marginRight, marginBottom)
            };

            // Row 0: Jadval va sarlavhalar (Star - hamma joyni egallaydi)
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            // Row 1: Footer (Auto - faqat o'zi uchun joy oladi)
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var contentStack = new StackPanel();

            // Sarlavha (faqat 1-betda)
            if (pageIndex == 0)
            {
                contentStack.Children.Add(new TextBlock
                {
                    Text = "Mavjud mahsulotlar qoldig'i",
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 5)
                });

                contentStack.Children.Add(new TextBlock
                {
                    Text = $"Sana: {DateTime.Today:dd.MM.yyyy}",
                    FontSize = 14,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                });
            }

            var table = new Grid();
            double[] widths = { 35, 70, 130, 70, 70, 70, 70, 80, 135 };
            foreach (var w in widths) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });

            AddRow(table, true, "T/r", "Kodi", "Nomi", "Razmer", "Qop soni", "Donasi", "Jami", "Narxi", "Umumiy");

            foreach (int itemIdx in pagesMapping[pageIndex])
            {
                var x = items[itemIdx];
                AddRow(table, false, (itemIdx + 1).ToString(), x.Code, x.Name, x.Type,
                       x.BundleCount?.ToString() ?? "0", x.BundleItemCount.ToString(),
                       x.TotalCount.ToString("N0"), x.UnitPrice.ToString("N2"), x.TotalAmount.ToString("N2"));
            }

            if (pageIndex == totalPages - 1)
            {
                AddRow(table, true, "", "JAMI:", "", "", "", "", "", "", totalSum.ToString("N2"));
            }

            contentStack.Children.Add(table);
            Grid.SetRow(contentStack, 0);
            mainGrid.Children.Add(contentStack);

            // Sahifa raqami (Footer) - Pastda qat'iy "mixlangan"
            var footerPageNum = new TextBlock
            {
                Text = string.Format("{0} - bet / {1}", pageIndex + 1, totalPages),
                FontSize = 11,
                Foreground = Brushes.DarkGray,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 0) // Marginni olib tashladik, masofani RowDefinition hal qiladi
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
            // ⭐ Almashtirilgan TextAlignment
            TextAlignment align =
                isHeader ? TextAlignment.Center : i switch
                {
                    0 => TextAlignment.Right,   // T/r – O'NG
                    1 => TextAlignment.Center,  // Kodi – O'RTA
                    2 => TextAlignment.Left,   // Nomi – O'NG
                    5 => TextAlignment.Center,  // Donasi – O'RTA
                    6 => TextAlignment.Right,   // Jami – O'NG
                    7 => TextAlignment.Right,   // Narxi – O'NG
                    8 => TextAlignment.Right,   // Umumiy – O'NG
                    _ => TextAlignment.Center   // Boshqa ustunlar – O'RTA
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