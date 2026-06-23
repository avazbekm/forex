namespace Forex.Wpf.Pages.Supply.ViewModels;

using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService.Enums;
using Forex.ClientService.Extensions;
using Forex.ClientService.Interfaces;
using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.ClientService.Models.Responses;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.ViewModels;
using MapsterMapper;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;

public partial class SupplyPageViewModel : ViewModelBase
{
    private readonly IApiSupplies suppliesApi;
    private readonly IApiUser usersApi;
    private readonly IApiCurrency currenciesApi;
    private readonly IMapper mapper;
    private readonly List<SupplyViewModel> allSupplies = [];

    public SupplyPageViewModel(
        IApiSupplies suppliesApi,
        IApiUser usersApi,
        IApiCurrency currenciesApi,
        IMapper mapper)
    {
        this.suppliesApi = suppliesApi;
        this.usersApi = usersApi;
        this.currenciesApi = currenciesApi;
        this.mapper = mapper;

        _ = LoadDataAsync();
    }

    [ObservableProperty] private DateTime date = DateTime.Today;
    [ObservableProperty] private SupplyPartyType selectedPartyType = SupplyPartyType.Supplier;
    [ObservableProperty] private UserViewModel? selectedUser;
    [ObservableProperty] private CurrencyViewModel? selectedCurrency;
    [ObservableProperty] private decimal? amount;
    [ObservableProperty] private string? description;
    [ObservableProperty] private DateTime filterBeginDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime filterEndDate = DateTime.Today;
    [ObservableProperty] private UserFilterOption selectedFilterUser = UserFilterOption.All;
    [ObservableProperty] private SupplyPartyFilter selectedSupplyFilter = SupplyPartyFilter.All;
    [ObservableProperty] private CurrencyFilterOption selectedCurrencyFilter = CurrencyFilterOption.All;
    [ObservableProperty] private SupplyViewModel? editingSupply;
    [ObservableProperty] private decimal filteredTotalAmount;
    [ObservableProperty] private decimal filteredSupplierAmount;
    [ObservableProperty] private decimal filteredConsolidatorAmount;
    [ObservableProperty] private int filteredCount;
    [ObservableProperty] private ObservableCollection<UserViewModel> availableSuppliers = [];
    [ObservableProperty] private ObservableCollection<UserViewModel> availableConsolidators = [];
    [ObservableProperty] private ObservableCollection<UserViewModel> availableUsers = [];
    [ObservableProperty] private ObservableCollection<UserFilterOption> availableFilterUsers = [];
    [ObservableProperty] private ObservableCollection<CurrencyViewModel> availableCurrencies = [];
    [ObservableProperty] private ObservableCollection<SupplyViewModel> supplies = [];

    public IReadOnlyList<SupplyPartyTypeOption> PartyTypes { get; } =
    [
        new(SupplyPartyType.Supplier, "Ta'minotchi"),
        new(SupplyPartyType.Consolidator, "Vositachi")
    ];

    public IReadOnlyList<SupplyPartyFilterOption> SupplyFilterOptions { get; } =
    [
        new(SupplyPartyFilter.All, "Barchasi"),
        new(SupplyPartyFilter.Consolidator, "Vositachi"),
        new(SupplyPartyFilter.Supplier, "Ta'minotchi")
    ];

    public IReadOnlyList<CurrencyFilterOption> CurrencyFilterOptions { get; } =
    [
        CurrencyFilterOption.All,
        CurrencyFilterOption.Uzs,
        CurrencyFilterOption.Usd
    ];

    public SupplyPartyTypeOption SelectedPartyTypeOption
    {
        get => PartyTypes.First(p => p.Value == SelectedPartyType);
        set => SelectedPartyType = value.Value;
    }

    public SupplyPartyFilterOption SelectedSupplyFilterOption
    {
        get => SupplyFilterOptions.First(p => p.Value == SelectedSupplyFilter);
        set => SelectedSupplyFilter = value.Value;
    }

    public bool IsSupplierMode => SelectedPartyType == SupplyPartyType.Supplier;
    public bool IsConsolidatorMode => SelectedPartyType == SupplyPartyType.Consolidator;
    public bool IsSupplyEditing => EditingSupply is not null;
    public string SubmitButtonText => IsSupplyEditing ? "Yangilash" : "Saqlash";
    partial void OnSelectedPartyTypeChanged(SupplyPartyType value)
    {
        AvailableUsers = value == SupplyPartyType.Supplier ? AvailableSuppliers : AvailableConsolidators;
        SelectedUser = AvailableUsers.FirstOrDefault();
        Amount = null;
        SelectedCurrency = GetDefaultCurrency(value);
        OnPropertyChanged(nameof(IsSupplierMode));
        OnPropertyChanged(nameof(IsConsolidatorMode));
        OnPropertyChanged(nameof(SelectedPartyTypeOption));
    }

    partial void OnFilterBeginDateChanged(DateTime value) => ApplySupplyFilters();
    partial void OnFilterEndDateChanged(DateTime value) => ApplySupplyFilters();
    partial void OnSelectedFilterUserChanged(UserFilterOption value) => ApplySupplyFilters();
    partial void OnSelectedCurrencyFilterChanged(CurrencyFilterOption value) => ApplySupplyFilters();

    partial void OnEditingSupplyChanged(SupplyViewModel? value)
    {
        OnPropertyChanged(nameof(IsSupplyEditing));
        OnPropertyChanged(nameof(SubmitButtonText));
    }

    partial void OnSelectedSupplyFilterChanged(SupplyPartyFilter value)
    {
        ApplySupplyFilters();
        OnPropertyChanged(nameof(SelectedSupplyFilterOption));
    }

    private async Task LoadDataAsync()
    {
        await Task.WhenAll(
            LoadCurrenciesAsync(),
            LoadUsersAsync(),
            LoadSuppliesAsync());

        AvailableUsers = AvailableSuppliers;
        AvailableFilterUsers = new ObservableCollection<UserFilterOption>(
            new[] { UserFilterOption.All }
                .Concat(AvailableSuppliers.Concat(AvailableConsolidators).OrderBy(u => u.Name).Select(UserFilterOption.FromUser)));
        SelectedUser = AvailableUsers.FirstOrDefault();
        SelectedCurrency = GetDefaultCurrency(SelectedPartyType);
        SelectedCurrencyFilter = CurrencyFilterOption.All;
        ApplySupplyFilters();
    }

    private async Task LoadCurrenciesAsync()
    {
        var response = await currenciesApi.GetAllAsync().Handle(isLoading => IsLoading = isLoading);

        if (response.IsSuccess)
            AvailableCurrencies = mapper.Map<ObservableCollection<CurrencyViewModel>>(response.Data);
        else
            ErrorMessage = response.Message ?? "Valyuta turlarini yuklashda xatolik.";
    }

    private async Task LoadUsersAsync()
    {
        var request = new FilteringRequest
        {
            Filters = new()
            {
                ["role"] = ["in:Taminotchi,Vositachi"]
            }
        };

        Response<List<UserResponse>> response = await usersApi.Filter(request).Handle(isLoading => IsLoading = isLoading);

        if (!response.IsSuccess)
        {
            ErrorMessage = response.Message ?? "Ta'minotchilarni yuklashda xatolik.";
            return;
        }

        AvailableSuppliers = mapper.Map<ObservableCollection<UserViewModel>>(
            response.Data.Where(u => u.Role == UserRole.Taminotchi));
        AvailableConsolidators = mapper.Map<ObservableCollection<UserViewModel>>(
            response.Data.Where(u => u.Role == UserRole.Vositachi));
    }

    private async Task LoadSuppliesAsync()
    {
        var response = await suppliesApi.GetAllAsync().Handle(isLoading => IsLoading = isLoading);

        if (response.IsSuccess)
        {
            allSupplies.Clear();
            allSupplies.AddRange(mapper.Map<List<SupplyViewModel>>(response.Data));
            ApplySupplyFilters();
        }
        else
            WarningMessage = response.Message ?? "Ta'minot tarixini yuklashda xatolik.";
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (!Validate())
            return;

        var request = new SupplyRequest
        {
            Date = Date.Date == DateTime.Today ? DateTime.Now : Date,
            PartyType = SelectedPartyType,
            UserId = SelectedUser!.Id,
            Amount = Amount ?? 0,
            CurrencyId = SelectedCurrency!.Id,
            Description = Description
        };

        var isEditing = EditingSupply is not null;
        var isSuccess = false;
        string? responseMessage;

        if (isEditing)
        {
            var response = await suppliesApi.Update(EditingSupply!.Id, request).Handle(isLoading => IsLoading = isLoading);
            isSuccess = response.IsSuccess;
            responseMessage = response.Message;
        }
        else
        {
            var response = await suppliesApi.Create(request).Handle(isLoading => IsLoading = isLoading);
            isSuccess = response.IsSuccess;
            responseMessage = response.Message;
        }

        if (isSuccess)
        {
            SuccessMessage = isEditing ? "Ta'minot yangilandi." : "Ta'minot saqlandi.";
            ResetForm();
            await LoadSuppliesAsync();
        }
        else
        {
            ErrorMessage = responseMessage ?? "Ta'minotni saqlashda xatolik yuz berdi.";
        }
    }

    [RelayCommand]
    private void Edit(SupplyViewModel supply)
    {
        if (supply is null)
            return;

        EditingSupply = supply;
        Date = supply.Date;
        SelectedPartyType = supply.PartyType;
        AvailableUsers = SelectedPartyType == SupplyPartyType.Supplier ? AvailableSuppliers : AvailableConsolidators;
        SelectedUser = AvailableUsers.FirstOrDefault(u => u.Id == supply.User.Id);
        SelectedCurrency = AvailableCurrencies.FirstOrDefault(c => c.Id == supply.Currency.Id);
        Amount = supply.Amount;
        Description = supply.Description;
    }

    [RelayCommand]
    private async Task DeleteAsync(SupplyViewModel supply)
    {
        if (supply is null)
            return;

        if (!Confirm("Ta'minot yozuvini o'chirishni tasdiqlaysizmi? Balans ham qaytariladi."))
            return;

        var response = await suppliesApi.Delete(supply.Id).Handle(isLoading => IsLoading = isLoading);

        if (response.IsSuccess)
        {
            allSupplies.RemoveAll(s => s.Id == supply.Id);
            Supplies.Remove(supply);
            RecalculateSupplyTotals(Supplies);
            SuccessMessage = "Ta'minot yozuvi o'chirildi.";
        }
        else
        {
            ErrorMessage = response.Message ?? "Ta'minot yozuvini o'chirishda xatolik.";
        }
    }

    public void AddCreatedUser(UserViewModel? user)
    {
        if (user is null)
            return;

        var collection = SelectedPartyType == SupplyPartyType.Supplier
            ? AvailableSuppliers
            : AvailableConsolidators;

        collection.Add(user);
        AvailableUsers = collection;
        SelectedUser = user;
        AvailableFilterUsers.Add(UserFilterOption.FromUser(user));
    }

    [RelayCommand]
    private void ClearFilters()
    {
        FilterBeginDate = DateTime.Today.AddMonths(-1);
        FilterEndDate = DateTime.Today;
        SelectedFilterUser = UserFilterOption.All;
        SelectedSupplyFilter = SupplyPartyFilter.All;
        SelectedCurrencyFilter = CurrencyFilterOption.All;
    }

    private bool Validate()
    {
        if (SelectedUser is null)
        {
            WarningMessage = IsSupplierMode ? "Ta'minotchini tanlang." : "Vositachini tanlang.";
            return false;
        }

        if (SelectedCurrency is null)
        {
            WarningMessage = "Valyutani tanlang.";
            return false;
        }

        if (IsSupplierMode)
        {
            if (Amount is null or <= 0)
            {
                WarningMessage = "Summa 0 dan katta bo'lishi kerak.";
                return false;
            }
        }
        else if (Amount is null or <= 0)
        {
            WarningMessage = "Summa 0 dan katta bo'lishi kerak.";
            return false;
        }

        return true;
    }

    private void ResetForm()
    {
        Date = DateTime.Today;
        SelectedPartyType = SupplyPartyType.Supplier;
        AvailableUsers = AvailableSuppliers;
        SelectedUser = AvailableUsers.FirstOrDefault();
        Amount = null;
        Description = null;
        SelectedCurrency = GetDefaultCurrency(SelectedPartyType);
        EditingSupply = null;
    }

    private CurrencyViewModel? GetDefaultCurrency(SupplyPartyType partyType)
    {
        if (partyType == SupplyPartyType.Consolidator)
        {
            var usd = GetCurrencyByCode("USD");

            if (usd is not null)
                return usd;
        }

        return AvailableCurrencies.FirstOrDefault(c => c.IsDefault)
               ?? AvailableCurrencies.FirstOrDefault();
    }

    private CurrencyViewModel? GetCurrencyByCode(string code)
        => AvailableCurrencies.FirstOrDefault(c => c.Code.Equals(code, StringComparison.InvariantCultureIgnoreCase));

    private void ApplySupplyFilters()
    {
        if (FilterEndDate.Date < FilterBeginDate.Date)
            return;

        var filtered = allSupplies
            .Where(s => s.Date.Date >= FilterBeginDate.Date && s.Date.Date <= FilterEndDate.Date)
            .Where(s => SelectedFilterUser.User is null || s.User.Id == SelectedFilterUser.User.Id)
            .Where(s => SelectedSupplyFilter switch
            {
                SupplyPartyFilter.Supplier => s.PartyType == SupplyPartyType.Supplier,
                SupplyPartyFilter.Consolidator => s.PartyType == SupplyPartyType.Consolidator,
                _ => true
            })
            .Where(s => SelectedCurrencyFilter.Code is null || s.Currency.Code.Equals(SelectedCurrencyFilter.Code, StringComparison.InvariantCultureIgnoreCase))
            .OrderByDescending(s => s.Date)
            .ToList();

        Supplies = new ObservableCollection<SupplyViewModel>(filtered);
        RecalculateSupplyTotals(filtered);
    }

    private void RecalculateSupplyTotals(IEnumerable<SupplyViewModel> source)
    {
        var items = source.ToList();
        FilteredCount = items.Count;
        FilteredTotalAmount = items.Sum(s => s.Amount);
        FilteredSupplierAmount = items.Where(s => s.PartyType == SupplyPartyType.Supplier).Sum(s => s.Amount);
        FilteredConsolidatorAmount = items.Where(s => s.PartyType == SupplyPartyType.Consolidator).Sum(s => s.Amount);
    }

    [RelayCommand]
    private void ExportToExcel()
    {
        if (!Supplies.Any())
        {
            MessageBox.Show("Excelga eksport qilish uchun ma'lumot yo'q.", "Eslatma", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel fayllari (*.xlsx)|*.xlsx",
            FileName = $"Taminot_{FilterBeginDate:dd.MM.yyyy}-{FilterEndDate:dd.MM.yyyy}.xlsx"
        };

        if (dialog.ShowDialog() != true)
            return;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Ta'minot");
        string[] headers = ["T/r", "Sana", "Turi", "Shaxs", "Valyuta", "Summa", "Izoh"];

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Range(1, 1, 1, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);

        var row = 2;
        var index = 1;
        foreach (var supply in Supplies)
        {
            ws.Cell(row, 1).Value = index++;
            ws.Cell(row, 2).Value = supply.Date.ToString("dd.MM.yyyy HH:mm");
            ws.Cell(row, 3).Value = supply.PartyTypeText;
            ws.Cell(row, 4).Value = supply.User?.Name ?? "-";
            ws.Cell(row, 5).Value = supply.Currency?.Code ?? "-";
            ws.Cell(row, 6).Value = supply.Amount;
            ws.Cell(row, 7).Value = supply.Description;
            row++;
        }

        ws.Cell(row, 5).Value = "Jami:";
        ws.Cell(row, 6).Value = FilteredTotalAmount;
        ws.Range(row, 5, row, 6).Style.Font.SetBold();
        ws.Columns().AdjustToContents();
        workbook.SaveAs(dialog.FileName);
        MessageBox.Show("Excel fayl muvaffaqiyatli saqlandi.", "Tayyor", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

public sealed record SupplyPartyTypeOption(SupplyPartyType Value, string Text);

public enum SupplyPartyFilter
{
    All,
    Supplier,
    Consolidator
}

public sealed record SupplyPartyFilterOption(SupplyPartyFilter Value, string Text);

public sealed record CurrencyFilterOption(string? Code, string Text)
{
    public static CurrencyFilterOption All { get; } = new(null, "Barchasi");
    public static CurrencyFilterOption Uzs { get; } = new("UZS", "UZS");
    public static CurrencyFilterOption Usd { get; } = new("USD", "USD");
}

public sealed record UserFilterOption(UserViewModel? User, string Text)
{
    public static UserFilterOption All { get; } = new(null, "Barchasi");
    public static UserFilterOption FromUser(UserViewModel user) => new(user, user.Name);
}
