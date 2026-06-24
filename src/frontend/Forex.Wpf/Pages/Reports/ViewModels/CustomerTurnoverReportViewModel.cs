namespace Forex.Wpf.Pages.Reports.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService;
using Forex.ClientService.Enums;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.Resources.Charts;
using Forex.Wpf.ViewModels;
using PdfSharp.Drawing;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Forex.Wpf.Windows;

public partial class CustomerTurnoverReportViewModel : PagedReportViewModel<TurnoversViewModel>
{
    private readonly ForexClient _client;
    private readonly CommonReportDataService _commonData;
    private HashSet<long>? participantIds;
    private bool _suppress;

    [ObservableProperty] private UserViewModel? selectedCustomer;
    [ObservableProperty] private TurnoverPartyFilter selectedPartyFilter = TurnoverPartyFilter.All;
    [ObservableProperty] private DateTime beginDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime endDate = DateTime.Today;

    [ObservableProperty] private decimal summaryDebit;
    [ObservableProperty] private decimal summaryCredit;

    [ObservableProperty] private ChartData turnoverChart = new();

    partial void OnSelectedCustomerChanged(UserViewModel? value) { if (!_suppress) _ = LoadDataAsync(); }
    partial void OnSelectedPartyFilterChanged(TurnoverPartyFilter value)
    {
        ApplyPartyFilter();
        OnPropertyChanged(nameof(SelectedPartyFilterOption));
    }

    partial void OnBeginDateChanged(DateTime value) { if (!_suppress) _ = OnDateRangeChangedAsync(); }
    partial void OnEndDateChanged(DateTime value) { if (!_suppress) _ = OnDateRangeChangedAsync(); }

    private async Task OnDateRangeChangedAsync()
    {
        await RebuildParticipantsAsync();
        await LoadDataAsync();
    }

    public ObservableCollection<UserViewModel> AvailableCustomers { get; } = [];

    public IReadOnlyList<TurnoverPartyFilterOption> PartyFilterOptions { get; } =
    [
        new(TurnoverPartyFilter.Customers, "Mijozlar"),
        new(TurnoverPartyFilter.Suppliers, "Ta'minotchilar"),
        new(TurnoverPartyFilter.Consolidators, "Vositachilar"),
        new(TurnoverPartyFilter.All, "Barchasi")
    ];

    public TurnoverPartyFilterOption SelectedPartyFilterOption
    {
        get => PartyFilterOptions.First(p => p.Value == SelectedPartyFilter);
        set => SelectedPartyFilter = value.Value;
    }


    public ObservableCollection<TurnoversViewModel> Operations { get; } = [];
    [ObservableProperty] private TurnoversViewModel? selectedItem;

    [ObservableProperty] private decimal _beginBalance;
    [ObservableProperty] private decimal _lastBalance;
    [ObservableProperty] private string? _settlementCurrencyCode;

    public bool HasData => Operations?.Count > 0;

    public CustomerTurnoverReportViewModel(ForexClient client, CommonReportDataService commonData)
    {
        _client = client;
        _commonData = commonData;

        Operations.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasData));
        _commonData.AvailableCustomers.CollectionChanged += OnCommonCustomersChanged;
        ApplyPartyFilter();
    }


    #region Load Data

    private async Task LoadDataAsync()
    {
        if (SelectedCustomer is null)
        {
            Operations.Clear();
            SetSource(Operations);
            TurnoverChart = new();
            BeginBalance = 0;
            LastBalance = 0;
            SummaryDebit = 0;
            SummaryCredit = 0;
            return;
        }

        Operations.Clear();

        var requset = new TurnoverRequest
        (
            UserId: SelectedCustomer.Id,
            Begin: BeginDate,
            End: EndDate
        );

        var response = await _client.OperationRecords
            .GetTurnover(requset)
            .Handle(l => IsLoading = l);

        if (!response.IsSuccess || response.Data is null)
        {
            BeginBalance = 0;
            LastBalance = 0;
            return;
        }


        var data = response.Data;


        BeginBalance = data.BeginBalance;
        LastBalance = data.EndBalance;
        SettlementCurrencyCode = data.SettlementCurrencyCode;

        foreach (var op in data.OperationRecords.OrderBy(o => o.Date))
        {
            var amount = op.SettlementAmount;
            decimal debit = 0;
            decimal credit = 0;

            if (op.Type == ClientService.Enums.OperationType.Sale)
            {
                debit = Math.Abs(amount);
            }
            else if (op.Type == ClientService.Enums.OperationType.Transaction)
            {
                if (op.Transaction is not null)
                {
                    credit = op.Transaction.IsIncome == true ? Math.Abs(amount) : 0;
                    debit = op.Transaction.IsIncome == false ? Math.Abs(amount) : 0;
                }
                else
                {
                    debit = amount < 0 ? Math.Abs(amount) : 0;
                    credit = amount > 0 ? Math.Abs(amount) : 0;
                }
            }
            else if (op.Type == ClientService.Enums.OperationType.Supply)
            {
                credit = amount > 0 ? amount : 0;
                debit = amount < 0 ? Math.Abs(amount) : 0;
            }

            Operations.Add(new TurnoversViewModel
            {
                Id = op.Id,
                Date = op.Date.ToLocalTime(),
                Description = op.Description ?? "Izoh yo‘q",
                Debit = debit,
                Credit = credit
            });
        }

        SummaryDebit = Operations.Sum(x => x.Debit);
        SummaryCredit = Operations.Sum(x => x.Credit);
        SetSource(Operations);

        var byDay = Operations.GroupBy(o => o.Date.Date).OrderBy(g => g.Key).ToList();
        TurnoverChart = new ChartData
        {
            Labels = [.. byDay.Select(g => g.Key.ToString("dd.MM"))],
            Series =
            [
                new ChartSeries { Name = "Kirim", Color = Color.FromRgb(0x1B, 0x7A, 0x3E), Values = [.. byDay.Select(g => (double)g.Sum(o => o.Credit))] },
                new ChartSeries { Name = "Chiqim", Color = Color.FromRgb(0xC6, 0x28, 0x28), Values = [.. byDay.Select(g => (double)g.Sum(o => o.Debit))] }
            ]
        };
    }

    private async Task RebuildParticipantsAsync()
    {
        var ids = new HashSet<long>();
        var range = new Dictionary<string, List<string>>
        {
            ["date"] = [$">={BeginDate:o}", $"<{EndDate.AddDays(1):o}"]
        };

        try
        {
            var salesReq = new FilteringRequest { Filters = new(range) { ["customer"] = ["include"] } };
            var sres = await _client.Sales.Filter(salesReq).Handle();
            if (sres.IsSuccess && sres.Data is not null)
                foreach (var s in sres.Data.Where(s => s.Customer is not null))
                    ids.Add(s.Customer.Id);

            var txReq = new FilteringRequest { Filters = new(range) };
            var tres = await _client.Transactions.Filter(txReq).Handle();
            if (tres.IsSuccess && tres.Data is not null)
                foreach (var t in tres.Data)
                    ids.Add(t.UserId);

            var supRes = await _client.Supplies.GetAllAsync().Handle();
            if (supRes.IsSuccess && supRes.Data is not null)
                foreach (var sup in supRes.Data.Where(x =>
                    x.Date.ToLocalTime().Date >= BeginDate.Date &&
                    x.Date.ToLocalTime().Date <= EndDate.Date))
                    ids.Add(sup.UserId);
        }
        catch
        {
        }

        participantIds = ids;
        ApplyPartyFilter();
    }

    #endregion Load Data


    #region Commands

    [RelayCommand]
    private async Task OnTabSelectedAsync()
    {
        // Agar mijoz tanlangan bo'lsa va ma'lumotlar hali yuklanmagan bo'lsa (yoki yangilash kerak bo'lsa)
        if (SelectedCustomer is not null)
        {
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private void Preview()
    {
        if (Operations.Count == 0) return;

        var doc = CreateFixedDocument();
        ShowPreviewWindow(doc);
    }

    [RelayCommand]
    private void Print()
    {
        var doc = CreateFixedDocument();
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() == true)
        {
            printDialog.PrintDocument(doc.DocumentPaginator, $"Mijoz hisoboti - {SelectedCustomer?.Name}");
        }
    }

    [RelayCommand]
    private void ExportToExcel()
    {
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel fayllari (*.xlsx)|*.xlsx",
            FileName = $"Hisobot_{SelectedCustomer?.Name.Replace(" ", "_")}_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.xlsx"
        };

        if (saveDialog.ShowDialog() != true) return;

        try
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add("Mijoz hisoboti");

            int row = 1;

            // Sarlavha
            ws.Cell(row, 1).Value = "MIJOZ OPERATSIYALARI HISOBOTI";
            ws.Range(row, 1, row, 4).Merge().Style
                .Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(0, 102, 204))
                .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
            row += 2;

            // Mijoz va davr
            ws.Cell(row, 1).Value = $"Mijoz: {SelectedCustomer?.Name.ToUpper()}";
            ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(14);
            row++;
            ws.Cell(row, 1).Value = $"Davr: {BeginDate:dd.MM.yyyy} — {EndDate:dd.MM.yyyy}";
            ws.Cell(row, 1).Style.Font.SetFontSize(13);
            row += 2;

            // Header
            string[] headers = { "Sana", "Chiqim", "Kirim", "Izoh" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
                ws.Cell(row, i + 1).Style.Font.SetBold().Font.SetFontSize(13)
                    .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center)
                    .Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(240, 248, 255));
            }
            row++;

            // Boshlang‘ich qoldiq
            ws.Cell(row, 1).Value = "Boshlang‘ich qoldiq";
            ws.Range(row, 1, row, 3).Merge().Style
                .Font.SetBold().Font.SetFontSize(14)
                .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 4).Value = $"{BeginBalance:N2} {SettlementCurrencyCode}".Trim();
            ws.Cell(row, 4).Style.Font.SetBold().Font.SetFontSize(15).Font.SetFontColor(ClosedXML.Excel.XLColor.DarkBlue)
                .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Right);
            row++;

            // Operatsiyalar
            foreach (var op in Operations)
            {
                ws.Cell(row, 1).Value = op.Date.ToString("dd.MM.yyyy");
                ws.Cell(row, 1).Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

                if (op.Debit > 0)
                    ws.Cell(row, 2).Value = op.Debit.ToString("N0");
                if (op.Credit > 0)
                    ws.Cell(row, 3).Value = op.Credit.ToString("N0");

                ws.Cell(row, 4).Value = op.Description;

                ws.Cell(row, 2).Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Right);
                ws.Cell(row, 3).Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Right);
                ws.Cell(row, 4).Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Left);

                row++;
            }

            // Jami
            var totalDebit = Operations.Sum(x => x.Debit);
            var totalCredit = Operations.Sum(x => x.Credit);
            ws.Cell(row, 1).Value = "JAMI";
            ws.Cell(row, 1).Style.Font.SetBold();
            if (totalDebit > 0) ws.Cell(row, 2).Value = totalDebit.ToString("N0");
            if (totalCredit > 0) ws.Cell(row, 3).Value = totalCredit.ToString("N0");
            ws.Range(row, 1, row, 4).Style.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);
            row++;

            // Oxirgi qoldiq
            ws.Cell(row, 1).Value = "Oxirgi qoldiq";
            ws.Range(row, 1, row, 3).Merge().Style
                .Font.SetBold().Font.SetFontSize(15)
                .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 4).Value = $"{LastBalance:N2} {SettlementCurrencyCode}".Trim();
            ws.Cell(row, 4).Style.Font.SetBold().Font.SetFontSize(18)
                .Font.SetFontColor(LastBalance >= 0 ? ClosedXML.Excel.XLColor.DarkGreen : ClosedXML.Excel.XLColor.DarkRed)
                .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Right);

            // Avto kenglik
            ws.Columns().AdjustToContents();

            workbook.SaveAs(saveDialog.FileName);
            MessageBox.Show("Excel fayl muvaffaqiyatli saqlandi!", "Tayyor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Excel yaratishda xatolik: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ClearFilter()
    {
        _suppress = true;
        SelectedPartyFilter = TurnoverPartyFilter.All;
        SelectedCustomer = null;
        BeginDate = DateTime.Today.AddMonths(-1);
        EndDate = DateTime.Today;
        _suppress = false;
        Operations.Clear();
        SetSource(Operations);
        BeginBalance = 0;
        LastBalance = 0;
        SummaryDebit = 0;
        SummaryCredit = 0;
        _ = RebuildParticipantsAsync();
    }

    #endregion Commands

    #region Private Helpers

    private void OnCommonCustomersChanged(object? sender, NotifyCollectionChangedEventArgs e) => ApplyPartyFilter();

    private void ApplyPartyFilter()
    {
        var selectedId = SelectedCustomer?.Id;
        var users = _commonData.AvailableCustomers
            .Where(MatchesPartyFilter)
            .Where(u => participantIds is null || participantIds.Contains(u.Id))
            .OrderBy(u => u.Name)
            .ToList();

        AvailableCustomers.Clear();
        foreach (var user in users)
            AvailableCustomers.Add(user);

        _suppress = true;
        SelectedCustomer = selectedId is null
            ? null
            : AvailableCustomers.FirstOrDefault(u => u.Id == selectedId.Value);
        _suppress = false;
    }

    private bool MatchesPartyFilter(UserViewModel user) => SelectedPartyFilter switch
    {
        TurnoverPartyFilter.Customers => user.Role == UserRole.Mijoz,
        TurnoverPartyFilter.Suppliers => user.Role == UserRole.Taminotchi,
        TurnoverPartyFilter.Consolidators => user.Role == UserRole.Vositachi,
        _ => user.Role is UserRole.Mijoz or UserRole.Taminotchi or UserRole.Vositachi
    };

    private void ShowPreviewWindow(FixedDocument doc)
    {
        var viewer = new DocumentViewer { Document = doc, Margin = new Thickness(15) };

        var toolbar = new StackPanel
        {
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
                    FileName = $"Hisobot_{SelectedCustomer?.Name.Replace(" ", "_")}_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.pdf",
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
                    savedPdfPath = Path.Combine(folder, $"Hisobot_{SelectedCustomer?.Name.Replace(" ", "_")}_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.pdf");
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
                if (SelectedCustomer is null) return;

                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string folder = Path.Combine(docs, "Forex");
                Directory.CreateDirectory(folder);

                string fileName = $"Hisobot_{SelectedCustomer.Name.Replace(" ", "_")}_{BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.pdf";
                string path = Path.Combine(folder, fileName);

                SaveFixedDocumentToPdf(doc, path, 96);

                if (File.Exists(path))
                {
                    var window = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);
                    var viewModel = App.AppHost!.Services.GetRequiredService<TelegramShareViewModel>();
                    viewModel.PdfFilePath = path;
                    viewModel.MessageCaption = $"Mijoz aylanma hisoboti\nMijoz: {SelectedCustomer.Name}\nDavr: {BeginDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}";

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

        toolbar.Children.Add(saveButton);
        toolbar.Children.Add(openButton);
        toolbar.Children.Add(shareButton);

        var layout = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        layout.Children.Add(toolbar);
        layout.Children.Add(viewer);

        var previewWindow = new Window
        {
            Title = "Mijoz aylanma hisoboti - Ko'rish",
            Width = 1000,
            Height = 800,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = layout,
            Icon = Application.Current.MainWindow?.Icon,
            Owner = Application.Current.MainWindow,
            ShowInTaskbar = false
        };
        
        previewWindow.ShowDialog();
    }

    private FixedDocument CreateFixedDocument()
    {
        var doc = new FixedDocument(); // Shu ye
        // ... (Konstantalar va boshlang'ich hisob-kitoblar o'zgarmadi) ...
        // Konstanta qiymatlar
        const double pageWidth = 793.7;
        const double pageHeight = 1122.5;
        const double margin = 50;
        const double workingWidth = pageWidth - 2 * margin;
        const double initialHeaderHeight = 120;
        const double footerHeight = 25;
        const double balanceRowHeight = 45;
        const double headerHeight = 45;
        const double totalGridHeight = 45;
        const double lastBalanceHeight = 45;
        const double minFinalSpace = 20; // JAMI va Qoldiq qatorlari orasidagi minimal bo'shliq

        // Ustun kengliklari
        double[] finalColWidths = { 80, 100, 100, 413.7 }; // Jami 693.7

        var allOperations = Operations.ToList();
        int currentIndex = 0;
        int pageNumber = 1;

        // Jami debit/kreditni bir marta hisoblab olamiz
        var totalDebit = Operations.Sum(x => x.Debit);
        var totalCredit = Operations.Sum(x => x.Credit);

        // Yagona ma'lumot qatorining minimal balandligi (taxminan 45px)
        // JAMI qatori uchun ham shu balandlik ishlatiladi
        const double requiredSpaceForFinalRows = totalGridHeight + lastBalanceHeight + 2 * minFinalSpace;


        // Har bir sahifani yaratish uchun tsikl
        while (currentIndex < allOperations.Count || pageNumber == 1)
        {
            var page = new FixedPage { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
            var container = new StackPanel { Margin = new Thickness(margin) };
            double currentY = 0;
            bool isFirstPage = (pageNumber == 1);

            // 1. HEADER (Sarlavha, Mijoz, Davr)
            if (isFirstPage)
            {
                container.Children.Add(CreateTitleAndInfo(SelectedCustomer?.Name!, BeginDate, EndDate));
                currentY += initialHeaderHeight;
            }

            // 2. Boshlang'ich qoldiq (Faqat 1-sahifada)
            if (isFirstPage)
            {
                var initialBalanceGrid = CreateBalanceRow(finalColWidths, "Boshlang‘ich qoldiq", $"{BeginBalance:N2} {SettlementCurrencyCode}".Trim());
                container.Children.Add(initialBalanceGrid);
                currentY += balanceRowHeight;
            }

            // 3. Header qatori (Har bir sahifada)
            var headerGrid = CreateRow(finalColWidths, true, "Sana", "Chiqim", "Kirim", "Izoh");
            container.Children.Add(headerGrid);
            currentY += headerHeight;

            // Sahifada joriy ma'lumot qatorlari uchun mavjud balandlik
            double availableHeight = pageHeight - 2 * margin - currentY - footerHeight;

            int rowsAddedOnPage = 0;

            while (currentIndex < allOperations.Count)
            {
                var op = allOperations[currentIndex];

                // Operatsiya qatorini yaratish (balandligini aniqlash uchun)
                string debit = op.Debit > 0 ? op.Debit.ToString("N0") : "";
                string credit = op.Credit > 0 ? op.Credit.ToString("N0") : "";

                var operationRowGrid = CreateRow(finalColWidths, false,
                    op.Date.ToString("dd.MM.yyyy"),
                    debit,
                    credit,
                    op.Description
                );

                // Qatorning haqiqiy balandligini o'lchash
                operationRowGrid.Measure(new Size(workingWidth, double.MaxValue));
                operationRowGrid.Arrange(new Rect(0, 0, workingWidth, operationRowGrid.DesiredSize.Height));
                double requiredHeight = operationRowGrid.DesiredSize.Height;

                // 🛑 ENG MUHIM QISM: OXIRGI QATORLARGA JOY QOLDIRISHNI TEKSHIRISH
                bool isLastOperation = (currentIndex == allOperations.Count - 1);

                if (isLastOperation)
                {
                    // Agar bu oxirgi operatsiya bo'lsa, JAMI va Qoldiq uchun joy yetarlimi tekshiramiz.
                    double neededSpace = requiredHeight + requiredSpaceForFinalRows;

                    if (neededSpace > availableHeight && rowsAddedOnPage > 0)
                    {
                        // Oxirgi operatsiya va oxirgi qoldiqlar sig'masa,
                        // ushbu operatsiyani keyingi sahifaga qoldiramiz.
                        break;
                    }
                }
                // Agar operatsiya qatori o'zi ham sig'masa, keyingi sahifaga o'tamiz
                else if (requiredHeight > availableHeight && rowsAddedOnPage > 0)
                {
                    break;
                }


                // Operatsiya qatorini qo'shish
                container.Children.Add(operationRowGrid);
                currentY += requiredHeight;
                availableHeight -= requiredHeight;
                currentIndex++;
                rowsAddedOnPage++;
            }

            bool isLastPage = (currentIndex >= allOperations.Count);

            if (isLastPage)
            {
                var totalGrid = CreateRow(finalColWidths, true, "JAMI",
                    totalDebit > 0 ? totalDebit.ToString("N0") : "",
                    totalCredit > 0 ? totalCredit.ToString("N0") : "",
                    "");
                container.Children.Add(totalGrid);

                var lastBalanceGrid = CreateBalanceRow(finalColWidths, "Oxirgi qoldiq", $"{LastBalance:N2} {SettlementCurrencyCode}".Trim());
                container.Children.Add(lastBalanceGrid);
            }

            if (allOperations.Count == 0 && isFirstPage)
            {
                var totalGrid = CreateRow(finalColWidths, true, "JAMI", "", "", "");
                container.Children.Add(totalGrid);

                var lastBalanceGrid = CreateBalanceRow(finalColWidths, "Oxirgi qoldiq", $"{BeginBalance:N2} {SettlementCurrencyCode}".Trim());
                container.Children.Add(lastBalanceGrid);
            }

            page.Children.Add(container);

            var pc = new PageContent();
            ((IAddChild)pc).AddChild(page);
            doc.Pages.Add(pc);

            pageNumber++;

            // Ma'lumot yo'q bo'lsa va bu birinchi sahifa bo'lsa, tsikldan chiqish
            if (allOperations.Count == 0 && isFirstPage) break;
        }

        // ... (Footerlarni yakuniy to'g'rilash mantiqi o'zgarmadi, bu juda muhim)
        int totalPages = doc.Pages.Count;
        for (int i = 0; i < totalPages; i++)
        {
            var p = (FixedPage)((PageContent)doc.Pages[i]).GetPageRoot(false);
            if (p is not null)
            {
                UpdatePageFooter(p, i + 1, totalPages);
            }
        }

        return doc;
    }
    private StackPanel CreateTitleAndInfo(string customerName, DateTime? beginDate, DateTime? endDate)
    {
        var stack = new StackPanel();

        // Sarlavha
        stack.Children.Add(new TextBlock
        {
            Text = "MIJOZ OPERATSIYALARI HISOBOTI",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20),
            Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204))
        });

        // Mijoz va davr
        stack.Children.Add(new TextBlock
        {
            Text = $"Mijoz: {customerName?.ToUpper() ?? "TANLANMAGAN"}",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        stack.Children.Add(new TextBlock
        {
            Text = $"Davr: {(beginDate?.ToString("dd.MM.yyyy") ?? "-")} — {(endDate?.ToString("dd.MM.yyyy") ?? "-")}",
            FontSize = 15,
            Margin = new Thickness(0, 0, 0, 10)
        });

        return stack;
    }

    private void UpdatePageFooter(FixedPage page, int currentPage, int totalPages)
    {
        const double margin = 50;

        // Avvalgi PageInfo elementini topishga harakat qilish (agar bor bo'lsa)
        // FixedPage.Children ni aylanib chiqish va topish.
        TextBlock existingPageInfo = null!;
        foreach (var child in page.Children.OfType<TextBlock>())
        {
            // FixedPage.SetRight/SetBottom orqali joylashganligini tekshirishning ishonchli usuli yo'q,
            // shuning uchun biz uni o'chirib, yangidan qo'shamiz.
            if (FixedPage.GetRight(child) == margin)
            {
                existingPageInfo = child;
                break;
            }
        }

        if (existingPageInfo is not null)
        {
            page.Children.Remove(existingPageInfo);
        }

        // Yangi Footer yaratish va joylashtirish
        var pageInfo = new TextBlock
        {
            Text = $"{currentPage}-bet / {totalPages}",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
        };

        FixedPage.SetRight(pageInfo, margin); // O'ng chetidan margin masofada
        FixedPage.SetBottom(pageInfo, 20);    // Pastki chetidan 20 piksel yuqorida

        page.Children.Add(pageInfo);
    }

    private Grid CreateRow(double[] widths, bool isHeader, params string[] cells)
    {
        var grid = new Grid();

        for (int i = 0; i < widths.Length; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[i]) });

        for (int i = 0; i < cells.Length; i++)
        {
            var tb = new TextBlock
            {
                Text = cells[i],
                Padding = new Thickness(8, 8, 8, 8),
                FontSize = isHeader ? 14 : 12,
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            // Header bo‘lsa — hammasi o‘rtada
            if (isHeader)
            {
                tb.HorizontalAlignment = HorizontalAlignment.Center;
                tb.TextAlignment = TextAlignment.Center;
            }
            else
            {
                // Oddiy qatorlarda:
                switch (i)
                {
                    case 0: // Sana
                        tb.HorizontalAlignment = HorizontalAlignment.Center;
                        tb.TextAlignment = TextAlignment.Center;
                        break;
                    case 1: // Kirim
                    case 2: // Chiqim
                        tb.HorizontalAlignment = HorizontalAlignment.Right;   // o‘ngga
                        tb.TextAlignment = TextAlignment.Right;
                        tb.Margin = new Thickness(0, 0, 15, 0); // biroz ichkariga suramiz
                        break;
                    case 3: // Izoh
                        tb.HorizontalAlignment = HorizontalAlignment.Left;    // chapga
                        tb.TextAlignment = TextAlignment.Left;
                        tb.Margin = new Thickness(10, 0, 0, 0);
                        break;
                }
            }

            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Background = isHeader ? new SolidColorBrush(Color.FromRgb(240, 248, 255)) : Brushes.White,
                Child = tb
            };

            Grid.SetColumn(border, i);
            grid.Children.Add(border);
        }

        return grid;
    }

    private Grid CreateBalanceRow(double[] widths, string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 10, 0, 10) };

        for (int i = 0; i < widths.Length; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[i]) });

        // 1. Label — 1-2-3 ustunni birlashtirib, o‘rtada
        var labelTb = new TextBlock
        {
            Text = label,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Foreground = Brushes.Navy
        };

        var labelBorder = new Border
        {
            BorderBrush = Brushes.DarkBlue,
            BorderThickness = new Thickness(1.5),
            Background = new SolidColorBrush(Color.FromRgb(230, 240, 255)),
            Child = labelTb
        };

        Grid.SetColumn(labelBorder, 0);
        Grid.SetColumnSpan(labelBorder, 3);
        grid.Children.Add(labelBorder);

        // 2. Qiymat — faqat 4-ustunda, o‘ngga surilgan, lekin ustun ichida markazda
        var valueTb = new TextBlock
        {
            Text = value,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Padding = new Thickness(0, 8, 20, 8),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,   // o‘ngga surish
            Foreground = label.Contains("Oxirgi")
                ? (LastBalance >= 0 ? Brushes.DarkGreen : Brushes.DarkRed)
                : Brushes.DarkBlue
        };

        var valueBorder = new Border
        {
            BorderBrush = Brushes.DarkBlue,
            BorderThickness = new Thickness(1.5),
            Background = Brushes.White,
            Child = valueTb
        };

        Grid.SetColumn(valueBorder, 3);
        grid.Children.Add(valueBorder);

        return grid;
    }

    private void SaveFixedDocumentToPdf(FixedDocument doc, string path, int dpi = 600) // ❗ DPI 300 qilib o'zgartirildi
    {
        try
        {
            // Agar fayl mavjud bo'lsa, uni o'chirish
            if (File.Exists(path)) File.Delete(path);

            using var pdfDoc = new PdfSharp.Pdf.PdfDocument();

            // Har bir FixedPage ni PDF sahifasiga o'tkazish
            foreach (var pageContent in doc.Pages)
            {
                var fixedPage = pageContent.GetPageRoot(false);
                if (fixedPage is null) continue;

                // 1. FixedPage Layout-ni yangilash
                // O'lchash (Measure) va joylashtirish (Arrange) orqali UI elementlarining haqiqiy o'lchamlarini olish
                fixedPage.Measure(new Size(fixedPage.Width, fixedPage.Height));
                fixedPage.Arrange(new Rect(0, 0, fixedPage.Width, fixedPage.Height));
                fixedPage.UpdateLayout();

                // 2. FixedPage-ni yuqori sifatli rasm (RenderTargetBitmap) ga render qilish

                // Koeffitsient (96 DPI ga nisbatan necha marta kattaroq)
                double scale = dpi / 96.0;

                var bitmap = new RenderTargetBitmap(
                    // Render qilinadigan rasmni piksel o'lchamlari
                    (int)(fixedPage.Width * scale),
                    (int)(fixedPage.Height * scale),
                    // DPI
                    dpi, dpi,
                    PixelFormats.Pbgra32);

                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    // Render qilishda scaling (masshtablash) qo'llash
                    dc.PushTransform(new ScaleTransform(scale, scale));
                    dc.DrawRectangle(new VisualBrush(fixedPage), null,
                        new Rect(0, 0, fixedPage.Width, fixedPage.Height));
                }
                bitmap.Render(dv);

                // 3. Rasmni PNG stream orqali MemoryStream ga saqlash
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                ms.Position = 0;

                // 4. PdfSharp yordamida PDF sahifasini yaratish
                var pdfPage = pdfDoc.AddPage();
                // A4 o'lchamlarini mm da o'rnatish
                pdfPage.Width = XUnit.FromMillimeter(210);
                pdfPage.Height = XUnit.FromMillimeter(297);

                // 5. Rasmni PDF sahifasiga joylashtirish
                using var xgfx = XGraphics.FromPdfPage(pdfPage);
                using var ximg = XImage.FromStream(ms);

                // Rasm va sahifa o'lchamlari nisbatini hisoblash
                double ratio = Math.Min(pdfPage.Width.Point / ximg.PointWidth, pdfPage.Height.Point / ximg.PointHeight);
                double w = ximg.PointWidth * ratio;
                double h = ximg.PointHeight * ratio;

                // Rasmni PDF sahifasining markaziga joylashtirish
                xgfx.DrawImage(ximg, (pdfPage.Width.Point - w) / 2, (pdfPage.Height.Point - h) / 2, w, h);
            }

            // PDF faylni saqlash
            pdfDoc.Save(path);
        }
        catch (Exception ex)
        {
            // Xatolik haqida xabar berish
            MessageBox.Show($"PDF saqlashda xatolik: {ex.Message}", "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion Private Helpers
}

public enum TurnoverPartyFilter
{
    All,
    Customers,
    Suppliers,
    Consolidators
}

public sealed record TurnoverPartyFilterOption(TurnoverPartyFilter Value, string Text);
