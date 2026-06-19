namespace Forex.Wpf.Pages.Transactions.ViewModels;

using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService;
using Forex.ClientService.Enums;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Forex.Wpf.Common.Services;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.ViewModels;
using MapsterMapper;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;

public partial class TransactionPageViewModel : ViewModelBase
{
    private readonly ForexClient client;
    private readonly IMapper mapper;

    // Edit qilinayotgan tranzaksiyaning original qiymati
    private decimal _originalTransactionNetAmount = 0;
    private TransactionViewModel? _originalTransaction = null;

    public TransactionPageViewModel(ForexClient client, IMapper mapper)
    {
        this.client = client;
        this.mapper = mapper;

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(BeginDate) or nameof(EndDate))
                _ = LoadTransactionsAsync();
        };

        Transaction.PropertyChanged += OnTransactionPropertyChanged;

        _ = LoadDataAsync();
    }

    [ObservableProperty] private ObservableCollection<TransactionViewModel> transactions = [];
    [ObservableProperty] private ObservableCollection<UserViewModel> availableUsers = [];
    [ObservableProperty] private ObservableCollection<UserViewModel> filteredUsers = [];
    [ObservableProperty] private string userFilterText = string.Empty;
    [ObservableProperty] private ObservableCollection<UserViewModel> filterPanelFilteredUsers = [];
    [ObservableProperty] private ObservableCollection<TransactionUserFilterOption> filterPanelUserOptions = [];
    [ObservableProperty] private string filterPanelUserText = string.Empty;
    [ObservableProperty] private ObservableCollection<CurrencyViewModel> availableCurrencies = [];
    [ObservableProperty] private TransactionViewModel transaction = new();
    [ObservableProperty] private TransactionUserFilterOption selectedFilterUser = TransactionUserFilterOption.All;
    [ObservableProperty] private UserRoleFilterOption selectedUserRoleFilter = UserRoleFilterOption.All;
    [ObservableProperty] private PaymentMethodFilterOption selectedPaymentMethodFilter = PaymentMethodFilterOption.All;

    [ObservableProperty] private TransactionTypeFilter selectedTransactionType = TransactionTypeFilter.All;

    public static IEnumerable<TransactionTypeFilter> TransactionTypes => Enum.GetValues<TransactionTypeFilter>();
    public IReadOnlyList<UserRoleFilterOption> UserRoleFilterOptions { get; } =
    [
        UserRoleFilterOption.All,
        new(UserRole.Mijoz, "Mijoz"),
        new(UserRole.Taminotchi, "Ta'minotchi"),
        new(UserRole.Vositachi, "Vositachi"),
        new(UserRole.Hodim, "Hodim")
    ];

    partial void OnSelectedTransactionTypeChanged(TransactionTypeFilter value) => _ = LoadTransactionsAsync();
    partial void OnSelectedPaymentMethodFilterChanged(PaymentMethodFilterOption value) => _ = LoadTransactionsAsync();
    partial void OnSelectedFilterUserChanged(TransactionUserFilterOption value) => _ = LoadTransactionsAsync();
    partial void OnSelectedUserRoleFilterChanged(UserRoleFilterOption value) => _ = LoadTransactionsAsync();

    public static IEnumerable<PaymentMethod> AvailablePaymentMethods => Enum.GetValues<PaymentMethod>();
    public IReadOnlyList<PaymentMethodFilterOption> AvailablePaymentMethodFilters { get; } =
    [
        PaymentMethodFilterOption.All,
        new(PaymentMethod.Naqd, "Naqd"),
        new(PaymentMethod.MobilIlova, "Mobil ilova"),
        new(PaymentMethod.Plastik, "Plastik"),
        new(PaymentMethod.HisobRaqam, "Hisob raqam")
    ];
    [ObservableProperty] private DateTime beginDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime endDate = DateTime.Today;

    [ObservableProperty] private bool isIncomeEnabled = true;
    [ObservableProperty] private bool isExpenseEnabled = true;
    [ObservableProperty] private bool isDiscountEnabled = false;
    [ObservableProperty] private decimal? totalAmountWithUserBalance;
    [ObservableProperty] private bool isDebtor;
    [ObservableProperty] private string submitButtonText = "To'lash";

    [ObservableProperty] private decimal totalIncome;
    [ObservableProperty] private decimal totalExpense;
    [ObservableProperty] private decimal netAmount;

    partial void OnUserFilterTextChanged(string value) => ApplyUserFilter(value);
    partial void OnFilterPanelUserTextChanged(string value) => ApplyFilterPanelUserFilter(value);

    public void ApplyUserFilter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            FilteredUsers = new ObservableCollection<UserViewModel>(AvailableUsers);
            return;
        }

        var filtered = AvailableUsers.Where(u =>
            TransliterationHelper.ContainsIgnoreScript(u.Name ?? "", text) ||
            TransliterationHelper.ContainsIgnoreScript(u.Phone ?? "", text) ||
            TransliterationHelper.ContainsIgnoreScript(u.Address ?? "", text));

        FilteredUsers = new ObservableCollection<UserViewModel>(filtered);
    }

    public void ApplyFilterPanelUserFilter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            FilterPanelFilteredUsers = new ObservableCollection<UserViewModel>(AvailableUsers);
            return;
        }

        var filtered = AvailableUsers.Where(u =>
            TransliterationHelper.ContainsIgnoreScript(u.Name ?? "", text) ||
            TransliterationHelper.ContainsIgnoreScript(u.Phone ?? "", text) ||
            TransliterationHelper.ContainsIgnoreScript(u.Address ?? "", text));

        FilterPanelFilteredUsers = new ObservableCollection<UserViewModel>(filtered);
    }

    partial void OnTransactionsChanged(ObservableCollection<TransactionViewModel> value)
    {
        CalculateTotals();
    }

    private void CalculateTotals()
    {
        TotalIncome = Transactions.Where(t => t.IsIncome).Sum(t => (t.Amount ?? 0m) * (t.ExchangeRate ?? 1m));
        TotalExpense = Transactions.Where(t => !t.IsIncome).Sum(t => (t.Amount ?? 0m) * (t.ExchangeRate ?? 1m));
        NetAmount = TotalIncome - TotalExpense;
    }

    #region Load Data

    private async Task LoadDataAsync()
    {
        await Task.WhenAll(
            LoadUsersAsync(),
            LoadCurrenciesAsync(),
            LoadTransactionsAsync()
        );
    }

    private async Task LoadTransactionsAsync()
    {
        FilteringRequest request = new()
        {
            Filters = new()
            {
                ["date"] = [$">={BeginDate:o}", $"<{EndDate.AddDays(1):o}"],
                ["user"] = ["include"],
                ["currency"] = ["include"],
            }
        };

        if (SelectedFilterUser.User != null)
        {
            request.Filters["userId"] = [$"={SelectedFilterUser.User.Id}"];
        }

        if (SelectedTransactionType != TransactionTypeFilter.All)
        {
            request.Filters["isIncome"] = [SelectedTransactionType == TransactionTypeFilter.Income ? "true" : "false"];
        }

        Response<List<TransactionResponse>> response = await client.Transactions.Filter(request)
            .Handle(isLoading => IsLoading = isLoading);

        if (response.IsSuccess)
        {
            List<TransactionResponse> ordered = response.Data.OrderByDescending(t => t.Date).ToList();

            if (SelectedFilterUser.User is not null)
            {
                ordered = ordered.Where(t => t.UserId == SelectedFilterUser.User.Id).ToList();
            }

            if (SelectedUserRoleFilter.Role is not null)
            {
                ordered = ordered.Where(t => t.User?.Role == SelectedUserRoleFilter.Role).ToList();
            }

            if (SelectedPaymentMethodFilter.Method.HasValue)
            {
                ordered = ordered.Where(t => t.PaymentMethod == SelectedPaymentMethodFilter.Method.Value).ToList();
            }
            
            Transactions = mapper.Map<ObservableCollection<TransactionViewModel>>(ordered);

            foreach (var trans in Transactions)
            {
                if (trans.IsIncome)
                    trans.Income = trans.Amount;
                else
                    trans.Expense = trans.Amount;
            }
            
            CalculateTotals();
        }
        else
        {
            WarningMessage = response.Message ?? "Tranzaksiyalarni yuklashda xatolik.";
        }
    }

    private async Task LoadCurrenciesAsync()
    {
        Response<List<CurrencyResponse>> response = await client.Currencies.GetAllAsync().Handle(isLoading => IsLoading = isLoading);

        if (response.IsSuccess)
        {
            AvailableCurrencies = mapper.Map<ObservableCollection<CurrencyViewModel>>(response.Data);
            Transaction.Currency = AvailableCurrencies.FirstOrDefault(c => c.IsDefault)!;
            if (Transaction.Currency is not null)
                Transaction.ExchangeRate = Transaction.Currency.ExchangeRate;
        }
        else
        {
            WarningMessage = response.Message ?? "Valyuta turlarini yuklashda xatolik.";
        }
    }

    private async Task LoadUsersAsync()
    {
        var response = await client.Users.GetAllAsync().Handle(isLoading => IsLoading = isLoading);

        if (response.IsSuccess)
        {
            // "admin" foydalanuvchisini filtrlash
            var filteredData = response.Data.Where(u => u.Username != "admin").ToList();

            // Filtrlangan ma'lumotni Map qilish
            AvailableUsers = mapper.Map<ObservableCollection<UserViewModel>>(filteredData);
            FilteredUsers = new ObservableCollection<UserViewModel>(AvailableUsers);
            FilterPanelFilteredUsers = new ObservableCollection<UserViewModel>(AvailableUsers);
            FilterPanelUserOptions = new ObservableCollection<TransactionUserFilterOption>(
                new[] { TransactionUserFilterOption.All }.Concat(AvailableUsers.OrderBy(u => u.Name).Select(TransactionUserFilterOption.FromUser)));
        }
        else
        {
            WarningMessage = response.Message ?? "Foydalanuvchilarni yuklashda xatolik.";
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task Submit()
    {
        if (!ValidateTransaction())
            return;

        // UI'dan backend modelga o'tkazish
        SyncTransactionFromUI();

        if (Transaction.Date.Date == DateTime.Today)
            Transaction.Date = DateTime.Now;

        TransactionRequest request = mapper.Map<TransactionRequest>(Transaction);

        if (IsEditing && Transaction.Id > 0)
        {
            var response = await client.Transactions.Update(request)
                .Handle(isLoading => IsLoading = isLoading);

            if (response.IsSuccess)
            {
                SuccessMessage = "Tranzaksiya muvaffaqiyatli yangilandi!";
                ResetTransaction();
            }
            else
            {
                ErrorMessage = response.Message ?? "Tranzaksiyani yangilashda xatolik yuz berdi.";
                return;
            }
        }
        else
        {
            var response = await client.Transactions.CreateAsync(request)
                .Handle(isLoading => IsLoading = isLoading);

            if (response.IsSuccess)
            {
                SuccessMessage = "To'lov muvaffaqiyatli amalga oshirildi.";
                ResetTransaction();
            }
            else
            {
                ErrorMessage = response.Message ?? "To'lovni amalga oshirishda xatolik yuz berdi.";
                return;
            }
        }

        await LoadDataAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        if (_originalTransaction != null)
        {
            // Original tranzaksiyani qaytarish
            Transactions.Insert(0, _originalTransaction);
        }

        ResetTransaction();
    }

    [RelayCommand]
    private async Task Edit(TransactionViewModel selectedTransaction)
    {
        if (selectedTransaction is null)
            return;

        bool hasCurrentData = Transaction.Income.HasValue ||
                             Transaction.Expense.HasValue ||
                             Transaction.User is not null;

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

        // PropertyChanged event'larni vaqtincha o'chirish
        Transaction.PropertyChanged -= OnTransactionPropertyChanged;

        try
        {
            // Original transaction qiymatini saqlash
            _originalTransactionNetAmount = (decimal)(selectedTransaction.Amount * selectedTransaction.ExchangeRate + selectedTransaction.Discount)!;
            _originalTransaction = selectedTransaction;

            // Ma'lumotlarni ko'chirish
            Transaction.Id = selectedTransaction.Id;
            Transaction.Date = selectedTransaction.Date;
            Transaction.IsIncome = selectedTransaction.IsIncome;
            Transaction.Amount = selectedTransaction.Amount;
            Transaction.ExchangeRate = selectedTransaction.ExchangeRate;
            Transaction.Discount = selectedTransaction.Discount;
            Transaction.PaymentMethod = selectedTransaction.PaymentMethod;
            Transaction.Description = selectedTransaction.Description;
            Transaction.DueDate = selectedTransaction.DueDate;

            // User'ni topish va o'rnatish
            Transaction.User = AvailableUsers.FirstOrDefault(u => u.Id == selectedTransaction.UserId)
                              ?? selectedTransaction.User;

            // Currency'ni topish va o'rnatish
            Transaction.Currency = AvailableCurrencies.FirstOrDefault(c => c.Id == selectedTransaction.CurrencyId)
                                  ?? selectedTransaction.Currency;

            // UI properties'ni o'rnatish
            if (Transaction.IsIncome)
            {
                Transaction.Income = Transaction.Amount;
                Transaction.Expense = null;
            }
            else
            {
                Transaction.Expense = Transaction.Amount;
                Transaction.Income = null;
            }

            // Edit rejimini yoqish
            IsEditing = true;
            SubmitButtonText = "Yangilash";

            // DataGrid'dan olib tashlash
            Transactions.Remove(selectedTransaction);
        }
        finally
        {
            // PropertyChanged event'ni qayta ulash
            Transaction.PropertyChanged += OnTransactionPropertyChanged;

            // Hisob-kitoblarni yangilash
            RecalculateAll();
        }
    }

    [RelayCommand]
    private async Task Delete(TransactionViewModel value)
    {
        if (value is null)
            return;

        var amount = value.IsIncome ? value.Income : value.Expense;
        var result = MessageBox.Show(
            $"Tranzaksiyani o'chirishni tasdiqlaysizmi?\n\nUser: {value.User?.Name}\nSumma: {amount:N2} {value.Currency?.Code}",
            "O'chirishni tasdiqlash",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.No)
            return;

        var response = await client.Transactions.Delete(value.Id)
            .Handle(isLoading => IsLoading = isLoading);

        if (response.IsSuccess)
        {
            Transactions.Remove(value);
            SuccessMessage = "Tranzaksiya muvaffaqiyatli o'chirildi";
            await LoadDataAsync();
        }
        else
        {
            ErrorMessage = response.Message ?? "Tranzaksiyani o'chirishda xatolik";
        }
    }

    [RelayCommand]
    private void ClearUserFilter()
    {
        SelectedFilterUser = TransactionUserFilterOption.All;
        FilterPanelUserText = string.Empty;
        ApplyFilterPanelUserFilter(string.Empty);
    }

    private void OnTransactionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Transaction.Income):
                OnIncomeChanged();
                break;
            case nameof(Transaction.Expense):
                OnExpenseChanged();
                break;
            case nameof(Transaction.Discount):
            case nameof(Transaction.User):
                RecalculateTotalAmountWithUserBalance();
                break;
            case nameof(Transaction.ExchangeRate):
                RecalculateAll();
                break;
            case nameof(Transaction.Currency):
                OnCurrencyChanged();
                break;
        }
    }

    #endregion

    #region Private Helpers

    private void ResetTransaction()
    {
        Transaction.PropertyChanged -= OnTransactionPropertyChanged;
        Transaction = new();
        Transaction.Currency = AvailableCurrencies.FirstOrDefault(c => c.IsDefault)!;
        if (Transaction.Currency is not null)
            Transaction.ExchangeRate = Transaction.Currency.ExchangeRate;
        Transaction.PropertyChanged += OnTransactionPropertyChanged;
        IsEditing = false;
        SubmitButtonText = "To'lash";
        _originalTransactionNetAmount = 0;
        _originalTransaction = null;

        // Input field'larni qayta yoqish
        IsIncomeEnabled = true;
        IsExpenseEnabled = true;
        IsDiscountEnabled = false;

        RecalculateAll();
    }

    private void SyncTransactionFromUI()
    {
        // UI'dan backend modelga sync qilish
        if (Transaction.Income.HasValue && Transaction.Income > 0)
        {
            Transaction.IsIncome = true;
            Transaction.Amount = Transaction.Income.Value;
        }
        else if (Transaction.Expense.HasValue && Transaction.Expense > 0)
        {
            Transaction.IsIncome = false;
            Transaction.Amount = Transaction.Expense.Value;
        }
    }

    private void OnIncomeChanged()
    {
        if (Transaction.Income is null || Transaction.Income == 0)
        {
            // Kirim bo'sh bo'lsa, ikkala field ham ochiq
            IsExpenseEnabled = true;
            IsDiscountEnabled = false;
        }
        else
        {
            // Kirim kiritilgan bo'lsa, chiqim yopiladi
            IsExpenseEnabled = false;
            IsDiscountEnabled = true;
            Transaction.Expense = null;
        }

        RecalculateAll();
    }

    private void OnExpenseChanged()
    {
        if (Transaction.Expense is null || Transaction.Expense == 0)
        {
            // Chiqim bo'sh bo'lsa, ikkala field ham ochiq
            IsIncomeEnabled = true;
            IsDiscountEnabled = false;
        }
        else
        {
            // Chiqim kiritilgan bo'lsa, kirim va chegirma yopiladi
            IsIncomeEnabled = false;
            IsDiscountEnabled = false;
            Transaction.Income = null;
            Transaction.Discount = null;
        }

        RecalculateAll();
    }

    private void OnCurrencyChanged()
    {
        if (Transaction.Currency is not null)
        {
            Transaction.Currency.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Transaction.Currency.ExchangeRate))
                {
                    Transaction.ExchangeRate = Transaction.Currency.ExchangeRate;
                    RecalculateAll();
                }
            };

            Transaction.ExchangeRate = Transaction.Currency.ExchangeRate;
        }

        RecalculateAll();
    }

    private void RecalculateAll()
    {
        RecalculateTotalAmountWithUserBalance();
    }

    private void RecalculateTotalAmountWithUserBalance()
    {
        if (Transaction.User is null)
        {
            TotalAmountWithUserBalance = null;
            IsDebtor = false;
            return;
        }

        // Joriy tranzaksiya qiymati (IsIncome=true bo'lsa +, false bo'lsa -)
        decimal currentAmount = Transaction.Income ?? -(Transaction.Expense ?? 0);
        decimal exchangeRate = (decimal)(Transaction.ExchangeRate > 0 ? Transaction.ExchangeRate : 1);
        decimal currentNetAmount = currentAmount * exchangeRate;
        decimal discount = Transaction?.Discount ?? 0;

        decimal total;

        if (IsEditing && Transaction?.Id > 0)
        {
            total = (decimal)Transaction.User.Balance! - _originalTransactionNetAmount + currentNetAmount + discount;
        }
        else
        {
            // Add rejimida: oddiy qo'shamiz
            total = (decimal)Transaction!.User.Balance! + currentNetAmount + discount;
        }

        TotalAmountWithUserBalance = total;
        IsDebtor = total < 0;
    }

    private bool ValidateTransaction()
    {
        if (!Transaction.Income.HasValue && !Transaction.Expense.HasValue)
        {
            WarningMessage = "Kirim yoki Chiqim kiritilishi kerak!";
            return false;
        }

        if (TotalAmountWithUserBalance < 0 &&
            (!Transaction.DueDate.HasValue || Transaction.DueDate < DateTime.Now))
        {
            WarningMessage = "Kamomat uchun to'lov muddati kiritilmagan yoki noto'g'ri formatda!";
            return false;
        }

        return true;
    }

    [RelayCommand]
    private void ExportToExcel()
    {
        if (!Transactions.Any())
        {
            MessageBox.Show("Excelga eksport qilish uchun ma'lumot yo‘q!", "Eslatma", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel fayllari (*.xlsx)|*.xlsx",
            FileName = $"Otkazmalar_{DateTime.Today:dd.MM.yyyy}.xlsx"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("O'tkazmalar");

            int row = 1;

            // Title
            ws.Cell(row, 1).Value = "O'TKAZMALAR HISOBOTI";
            ws.Range(row, 1, row, 8).Merge().Style
                .Font.SetBold().Font.SetFontSize(16)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row += 2;

            // Date
            ws.Cell(row, 1).Value = $"Davr: {BeginDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}";
            ws.Range(row, 1, row, 8).Merge().Style.Font.SetFontSize(12);
            row += 2;

            // Header
            string[] headers = { "T/r", "Sana", "User", "To'lov turi", "Valyuta", "Kurs", "Kirim", "Chiqim", "Izoh" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(row, i + 1).Value = headers[i];

            ws.Range(row, 1, row, headers.Length).Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.LightGray);
            row++;

            // Data
            int index = 1;
            foreach (var t in Transactions)
            {
                ws.Cell(row, 1).Value = index++;
                ws.Cell(row, 2).Value = t.Date.ToString("dd.MM.yyyy");
                ws.Cell(row, 3).Value = t.User?.Name ?? "-";
                ws.Cell(row, 4).Value = GetDescription(t.PaymentMethod);
                ws.Cell(row, 5).Value = t.Currency?.Name ?? "-";
                ws.Cell(row, 6).Value = t.ExchangeRate;
                ws.Cell(row, 7).Value = t.Income;
                ws.Cell(row, 8).Value = t.Expense;
                ws.Cell(row, 9).Value = t.Description;
                row++;
            }

            // Totals
            row++;
            ws.Cell(row, 6).Value = "JAMI:";
            ws.Cell(row, 6).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            
            ws.Cell(row, 7).Value = TotalIncome;
            ws.Cell(row, 7).Style.Font.SetBold().Font.SetFontColor(XLColor.Green);

            ws.Cell(row, 8).Value = TotalExpense;
            ws.Cell(row, 8).Style.Font.SetBold().Font.SetFontColor(XLColor.Red);

            ws.Columns().AdjustToContents();
            workbook.SaveAs(dialog.FileName);

            MessageBox.Show("Excel fayl muvaffaqiyatli saqlandi!", "Tayyor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Xatolik: {ex.Message}");
        }
    }

    private string GetDescription(Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        return field?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString();
    }

    #endregion
}

public sealed record TransactionUserFilterOption(UserViewModel? User, string Text)
{
    public static TransactionUserFilterOption All { get; } = new(null, "Barchasi");
    public static TransactionUserFilterOption FromUser(UserViewModel user) => new(user, user.Name);
}

public sealed record UserRoleFilterOption(UserRole? Role, string Text)
{
    public static UserRoleFilterOption All { get; } = new(null, "Barchasi");
}

public sealed record PaymentMethodFilterOption(PaymentMethod? Method, string Text)
{
    public static PaymentMethodFilterOption All { get; } = new(null, "Barchasi");
}
