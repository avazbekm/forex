namespace Forex.Wpf.Pages.Products.ViewModels;

using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Forex.ClientService;
using Forex.ClientService.Enums;
using Forex.ClientService.Extensions;
using Forex.ClientService.Models.Commons;
using Forex.ClientService.Models.Requests;
using Forex.Wpf.Common.Extensions;
using Forex.Wpf.Common.Services;
using Forex.Wpf.Common.Messages;
using Forex.Wpf.Pages.Common;
using Forex.Wpf.ViewModels;
using Mapster;
using MapsterMapper;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

public partial class ProductPageViewModel : ViewModelBase
{
    private readonly ForexClient client;
    private readonly IMapper mapper;
    private ProductEntryViewModel? backupEntry;
    private int backupIndex = -1;
    private bool _suppressFilter;
    private const int PageSize = 20;
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private int totalPages = 1;
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private ObservableCollection<object> visiblePageNumbers = [];
    [ObservableProperty] private int totalEntries;
    [ObservableProperty] private int totalStockCount;
    [ObservableProperty] private decimal totalStockValue;

    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;
    partial void OnCurrentPageChanged(int value) => UpdateVisiblePageNumbers();
    partial void OnTotalPagesChanged(int value) => UpdateVisiblePageNumbers();

    [ObservableProperty] private bool isNewProductMode;
    [ObservableProperty] private ProductViewModel currentProduct;
    [ObservableProperty] private DateTime? filterFromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime? filterToDate = DateTime.Today;
    [ObservableProperty] private string searchText = string.Empty;
    public const string AllSizesOption = "Barchasi";
    public static readonly ProductViewModel AllProductsOption = new() { Code = "Barchasi" };

    [ObservableProperty] private ProductViewModel? selectedFilterProduct = AllProductsOption;
    [ObservableProperty] private string filterProductText = string.Empty;
    [ObservableProperty] private ObservableCollection<ProductViewModel> filteredFilterProducts = [];
    [ObservableProperty] private ObservableCollection<ProductViewModel> availableFilterProducts = [];
    [ObservableProperty] private string? selectedSize = AllSizesOption;
    [ObservableProperty] private ObservableCollection<string> availableSizes = [];

    private readonly ObservableCollection<ProductEntryViewModel> _productEntries = [];
    public ICollectionView ProductEntriesView { get; }

    public ProductPageViewModel(IMapper mapper, ForexClient client)
    {
        this.mapper = mapper;
        this.client = client;
        CurrentProductEntry = new();
        CurrentProduct = new();
        ProductEntriesView = CollectionViewSource.GetDefaultView(_productEntries);
        ProductEntriesView.Filter = obj => obj is ProductEntryViewModel e && MatchesSearch(e);
        _ = LoadDataAsync();
    }

    [ObservableProperty] private ObservableCollection<ProductViewModel> availableProducts = [];
    [ObservableProperty] private ProductEntryViewModel? selectedProductEntry;
    [ObservableProperty] private string productType = string.Empty;
    [ObservableProperty] private string productCode = string.Empty;
    [ObservableProperty] private ProductEntryViewModel currentProductEntry;

    public static IEnumerable<ProductionOrigin> ProductionOrigins => Enum.GetValues<ProductionOrigin>();

    #region Loading Data

    private async Task LoadDataAsync()
    {
        await LoadProductsAsync();
        await Task.WhenAll(
            LoadProductEntriesAsync(),
            LoadProductSummaryAsync(),
            LoadFilterOptionsAsync());
    }

    private async Task LoadProductsAsync()
    {
        var response = await client.Products.GetAllAsync().Handle(l => IsLoading = l);
        if (response.IsSuccess)
            AvailableProducts = mapper.Map<ObservableCollection<ProductViewModel>>(response.Data);
        else ErrorMessage = response.Message ?? "Mahsulotlarni yuklashda xatolik!";
    }

    public void ApplyFilterProductSearch(string? text)
    {
        IEnumerable<ProductViewModel> results = AvailableFilterProducts;

        if (!string.IsNullOrWhiteSpace(text) && text.Trim() != AllProductsOption.Code)
        {
            var search = text.Trim();
            results = AvailableFilterProducts.Where(p =>
                TransliterationHelper.ContainsIgnoreScript(p.Code ?? string.Empty, search)
                || TransliterationHelper.ContainsIgnoreScript(p.Name ?? string.Empty, search));
        }

        FilteredFilterProducts = new ObservableCollection<ProductViewModel>(results.Prepend(AllProductsOption));
    }

    private FilteringRequest BuildFilterRequest(int page, int pageSize, bool applyTypeFilter = true)
    {
        FilteringRequest request = new()
        {
            Filters = new() { ["producttype"] = ["include:product"] },
            Descending = true,
            SortBy = "date",
            Page = page,
            PageSize = pageSize
        };

        if (FilterFromDate.HasValue || FilterToDate.HasValue)
        {
            var dateFilters = new List<string>();
            if (FilterFromDate.HasValue)
                dateFilters.Add($">={FilterFromDate.Value:dd.MM.yyyy}");
            if (FilterToDate.HasValue)
                dateFilters.Add($"<{FilterToDate.Value.AddDays(1):dd.MM.yyyy}");
            request.Filters["date"] = dateFilters;
        }

        if (applyTypeFilter)
        {
            var typeIds = GetFilteredProductTypeIds();
            if (typeIds is not null)
                request.Filters["productTypeId"] = [typeIds.Count > 0 ? "in:" + string.Join(",", typeIds) : "in:0"];
        }

        return request;
    }

    private List<long>? GetFilteredProductTypeIds()
    {
        var hasProduct = SelectedFilterProduct is not null && SelectedFilterProduct.Id > 0;
        var hasSize = !string.IsNullOrWhiteSpace(SelectedSize) && SelectedSize != AllSizesOption;
        if (!hasProduct && !hasSize)
            return null;

        IEnumerable<ProductTypeViewModel> types = hasProduct
            ? SelectedFilterProduct!.ProductTypes ?? []
            : AvailableProducts.SelectMany(p => p.ProductTypes ?? Enumerable.Empty<ProductTypeViewModel>());

        if (hasSize)
            types = types.Where(t => string.Equals(t.Type, SelectedSize, StringComparison.OrdinalIgnoreCase));

        return types.Select(t => t.Id).Where(id => id > 0).Distinct().ToList();
    }

    private async Task LoadFilterOptionsAsync()
    {
        var response = await client.ProductEntries.Filter(BuildFilterRequest(0, 0, applyTypeFilter: false)).Handle();
        if (response.IsSuccess && response.Data is not null)
        {
            var entries = mapper.Map<List<ProductEntryViewModel>>(response.Data);

            var sizes = entries
                .Select(e => e.ProductType?.Type)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
            AvailableSizes = new ObservableCollection<string>(sizes.Prepend(AllSizesOption));

            var productIds = entries
                .Select(e => e.ProductType?.Product?.Id ?? 0)
                .Where(id => id > 0)
                .ToHashSet();
            AvailableFilterProducts = new ObservableCollection<ProductViewModel>(
                AvailableProducts.Where(p => productIds.Contains(p.Id)));
            ApplyFilterProductSearch(null);
        }
    }

    private async Task LoadProductEntriesAsync()
    {
        var response = await client.ProductEntries.Filter(BuildFilterRequest(CurrentPage, PageSize)).Handle(l => IsLoading = l);
        if (response.IsSuccess)
        {
            _productEntries.Clear();
            _productEntries.AddRange(mapper.Map<ObservableCollection<ProductEntryViewModel>>(response.Data));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
        }
        else ErrorMessage = response.Message ?? "Kirim tarixini yuklashda xatolik!";
    }

    private async Task LoadProductSummaryAsync()
    {
        var response = await client.ProductEntries.Filter(BuildFilterRequest(0, 0)).Handle();
        if (response.IsSuccess && response.Data is not null)
        {
            var entries = mapper.Map<List<ProductEntryViewModel>>(response.Data);
            TotalEntries = entries.Count;
            TotalStockCount = entries.Sum(e => e.Count ?? 0);
            TotalStockValue = entries.Sum(e => (e.Count ?? 0) * (e.UnitPrice ?? 0));
            TotalCount = entries.Count;
            TotalPages = PageSize > 0 ? Math.Max(1, (int)Math.Ceiling((double)entries.Count / PageSize)) : 1;
        }
    }

    private void UpdateVisiblePageNumbers()
    {
        var pages = new List<object>();

        if (TotalPages <= 7)
        {
            for (int i = 1; i <= TotalPages; i++)
                pages.Add(i);
        }
        else
        {
            pages.Add(1);
            if (CurrentPage > 4) pages.Add("...");

            int start = Math.Max(2, CurrentPage - 1);
            int end = Math.Min(TotalPages - 1, CurrentPage + 1);
            if (CurrentPage < 5) end = 5;
            else if (CurrentPage > TotalPages - 4) start = TotalPages - 4;

            for (int i = start; i <= end; i++) pages.Add(i);

            if (CurrentPage < TotalPages - 3) pages.Add("...");
            pages.Add(TotalPages);
        }

        VisiblePageNumbers = new ObservableCollection<object>(pages);
    }

    partial void OnFilterFromDateChanged(DateTime? value) => _ = OnDateRangeChangedAsync();
    partial void OnFilterToDateChanged(DateTime? value) => _ = OnDateRangeChangedAsync();
    partial void OnSearchTextChanged(string value) => ProductEntriesView.Refresh();
    partial void OnSelectedFilterProductChanged(ProductViewModel? value) => _ = ReloadFromFirstPageAsync();
    partial void OnSelectedSizeChanged(string? value) => _ = ReloadFromFirstPageAsync();

    private async Task OnDateRangeChangedAsync()
    {
        if (_suppressFilter) return;
        _suppressFilter = true;
        SelectedFilterProduct = AllProductsOption;
        FilterProductText = string.Empty;
        SelectedSize = AllSizesOption;
        _suppressFilter = false;
        CurrentPage = 1;
        await Task.WhenAll(LoadProductEntriesAsync(), LoadProductSummaryAsync(), LoadFilterOptionsAsync());
    }

    private async Task ReloadFromFirstPageAsync()
    {
        if (_suppressFilter) return;
        CurrentPage = 1;
        await Task.WhenAll(LoadProductEntriesAsync(), LoadProductSummaryAsync());
    }

    [RelayCommand]
    private async Task ClearFilters()
    {
        _suppressFilter = true;
        FilterFromDate = DateTime.Today.AddDays(-7);
        FilterToDate = DateTime.Today;
        SearchText = string.Empty;
        SelectedFilterProduct = AllProductsOption;
        FilterProductText = string.Empty;
        ApplyFilterProductSearch(null);
        SelectedSize = AllSizesOption;
        _suppressFilter = false;
        CurrentPage = 1;
        await Task.WhenAll(LoadProductEntriesAsync(), LoadProductSummaryAsync(), LoadFilterOptionsAsync());
    }

    [RelayCommand] private async Task GoToFirstPage() { if (CurrentPage == 1) return; CurrentPage = 1; await LoadProductEntriesAsync(); }
    [RelayCommand] private async Task GoToPreviousPage() { if (!CanGoPrevious) return; CurrentPage--; await LoadProductEntriesAsync(); }
    [RelayCommand] private async Task GoToNextPage() { if (!CanGoNext) return; CurrentPage++; await LoadProductEntriesAsync(); }
    [RelayCommand] private async Task GoToLastPage() { if (CurrentPage == TotalPages) return; CurrentPage = TotalPages; await LoadProductEntriesAsync(); }

    [RelayCommand]
    private async Task GoToPage(object? parameter)
    {
        if (parameter is int page)
        {
            if (page < 1 || page > TotalPages || page == CurrentPage) return;
            CurrentPage = page;
            await LoadProductEntriesAsync();
        }
    }

    [RelayCommand]
    private async Task ExportToExcel()
    {
        var response = await client.ProductEntries.Filter(BuildFilterRequest(0, 0)).Handle(l => IsLoading = l);
        if (!response.IsSuccess || response.Data is null)
        {
            ErrorMessage = response.Message ?? "Ma'lumotlarni yuklashda xatolik.";
            return;
        }

        var list = mapper.Map<List<ProductEntryViewModel>>(response.Data);
        if (list.Count == 0)
        {
            MessageBox.Show("Excelga eksport qilish uchun ma'lumot yo'q.", "Eslatma", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel fayllari (*.xlsx)|*.xlsx",
            FileName = $"Mahsulot_kirimlari_{DateTime.Today:dd.MM.yyyy}.xlsx"
        };

        if (dialog.ShowDialog() != true)
            return;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Kirimlar");
        string[] headers = ["T/r", "Sana", "Kod", "Nomi", "Tayyorlanish usuli", "Razmer", "Qop soni", "Donasi", "Jami soni", "Tannarxi", "Jami summa"];

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Range(1, 1, 1, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);

        var row = 2;
        var index = 1;
        foreach (var e in list)
        {
            ws.Cell(row, 1).Value = index++;
            ws.Cell(row, 2).Value = e.Date.ToString("dd.MM.yyyy HH:mm");
            ws.Cell(row, 3).Value = e.ProductType?.Product?.Code ?? "-";
            ws.Cell(row, 4).Value = e.ProductType?.Product?.Name ?? "-";
            ws.Cell(row, 5).Value = e.ProductionOrigin?.ToString() ?? "-";
            ws.Cell(row, 6).Value = e.ProductType?.Type ?? "-";
            ws.Cell(row, 7).Value = e.BundleCount;
            ws.Cell(row, 8).Value = e.BundleItemCount;
            ws.Cell(row, 9).Value = e.Count;
            ws.Cell(row, 10).Value = e.UnitPrice;
            ws.Cell(row, 11).Value = (e.Count ?? 0) * (e.UnitPrice ?? 0);
            row++;
        }

        ws.Cell(row, 8).Value = "Jami:";
        ws.Cell(row, 9).Value = list.Sum(e => e.Count ?? 0);
        ws.Cell(row, 11).Value = list.Sum(e => (e.Count ?? 0) * (e.UnitPrice ?? 0));
        ws.Range(row, 8, row, 11).Style.Font.SetBold();
        ws.Columns().AdjustToContents();
        workbook.SaveAs(dialog.FileName);
        MessageBox.Show("Excel fayl muvaffaqiyatli saqlandi.", "Tayyor", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region Event Handlers

    partial void OnCurrentProductEntryChanged(ProductEntryViewModel? oldValue, ProductEntryViewModel newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= OnCurrentEntryPropertyChanged;

        if (newValue is not null)
            newValue.PropertyChanged += OnCurrentEntryPropertyChanged;
    }

    partial void OnCurrentProductChanged(ProductViewModel? oldValue, ProductViewModel newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= OnProductPropertyChanged;

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnProductPropertyChanged;
            CurrentProductEntry.ProductionOrigin = newValue.ProductionOrigin;

            if (ProductCode != newValue.Code)
                ProductCode = newValue.Code;
        }
    }

    private void OnCurrentEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProductEntryViewModel.BundleCount) or nameof(ProductEntryViewModel.BundleItemCount))
        {
            if (CurrentProductEntry.BundleCount.HasValue && CurrentProductEntry.BundleItemCount.HasValue)
                CurrentProductEntry.Count = CurrentProductEntry.BundleCount * CurrentProductEntry.BundleItemCount;
        }
    }

    private void OnProductPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProductViewModel.SelectedType) && CurrentProduct.SelectedType is not null)
            UpdateEntryCalculations();
    }

    partial void OnProductCodeChanged(string? oldValue, string newValue)
    {
        if (!string.IsNullOrEmpty(oldValue))
        {
            var toRemove = AvailableProducts.FirstOrDefault(c => c.Code == oldValue && c.Id < 1);
            if (toRemove != null)
            {
                AvailableProducts.Remove(toRemove);
            }
        }

        if (string.IsNullOrWhiteSpace(newValue) || (CurrentProduct != null && CurrentProduct.Code == newValue))
            return;

        var existing = AvailableProducts.FirstOrDefault(c => string.Equals(c.Code, newValue, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            CurrentProduct = existing;
            IsNewProductMode = false;
            return;
        }

        var byBarcode = BarcodeResolver.Resolve(AvailableProducts, newValue);
        if (byBarcode is not null)
        {
            IsNewProductMode = false;
            CurrentProduct = byBarcode.Product;
            byBarcode.Product.SelectedType = byBarcode.ProductType;
            ProductType = byBarcode.ProductType.Type;
            ProductCode = byBarcode.Product.Code;
            return;
        }

        if (Confirm($"'{newValue}' yangi mahsulot sifatida qo'shilsinmi?"))
        {
            CurrentProduct = new ProductViewModel { Code = newValue };
            IsNewProductMode = true;
            CurrentProduct.ImagePath = string.Empty;
        }
        else
        {
            ProductCode = string.Empty;
            WeakReferenceMessenger.Default.Send(new FocusControlMessage("ProductCode"));
        }
    }

    partial void OnProductTypeChanged(string? oldValue, string newValue)
    {
        var type = CurrentProduct.SelectedType;
        var types = CurrentProduct.ProductTypes;
        types.Remove(types.FirstOrDefault(c => c.Type == oldValue && c.Id < 1)!);

        if (string.IsNullOrWhiteSpace(newValue) || type?.Type == newValue) return;

        var existing = types.FirstOrDefault(c => string.Equals(c.Type, newValue, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) CurrentProduct.SelectedType = existing;
        else if (Confirm($"'{newValue}' yangi razmer sifatida qo'shilsinmi?"))
        {
            CurrentProduct.SelectedType = new() { Type = newValue };
            types.Add(CurrentProduct.SelectedType);

            CurrentProduct.ProductTypes = new ObservableCollection<ProductTypeViewModel>(types);
            OnPropertyChanged(nameof(CurrentProduct));
        }
        else { ProductType = string.Empty; WeakReferenceMessenger.Default.Send(new FocusControlMessage("ProductType")); }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void ScanFill(string? code)
    {
        var match = BarcodeResolver.Resolve(AvailableProducts, code);
        if (match is null)
        {
            WarningMessage = $"Shtrix-kod topilmadi: {code}";
            return;
        }

        IsNewProductMode = false;
        CurrentProduct = match.Product;
        match.Product.SelectedType = match.ProductType;
        ProductType = match.ProductType.Type;
        ProductCode = match.Product.Code ?? string.Empty;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!Validate()) return;

        if (CurrentProductEntry.Date.Date == DateTime.Today)
            CurrentProductEntry.Date = DateTime.Now;

        ProductEntryRequest request = mapper.Map<ProductEntryRequest>(CurrentProductEntry);
        request.Product = mapper.Map<ProductRequest>(CurrentProduct);

        if (CurrentProduct.SelectedType is not null)
            request.Product.ProductTypes = [CurrentProduct.SelectedType.Adapt<ProductTypeRequest>()];

        if (IsNewProductMode && !string.IsNullOrWhiteSpace(CurrentProduct.ImagePath))
        {
            var uploadedImagePath = await client.FileStorage.UploadFileAsync(CurrentProduct.ImagePath);
            if (uploadedImagePath is null)
            {
                ErrorMessage = "Rasm yuklashda xatolik! Qaytadan urinib ko'ring.";
                return;
            }
            request.Product.ImagePath = uploadedImagePath;
        }

        if (IsEditing && CurrentProductEntry.Id > 0)
        {
            var response = await client.ProductEntries.Update(request).Handle(l => IsLoading = l);
            if (response.IsSuccess)
            {
                var updatedEntry = mapper.Map<ProductEntryViewModel>(CurrentProductEntry);
                updatedEntry.Id = response.Data;
                updatedEntry.ProductType = CurrentProduct.SelectedType!;
                updatedEntry.ProductType.Product = CurrentProduct;

                if (backupIndex >= 0 && backupIndex < _productEntries.Count)
                    _productEntries[backupIndex] = updatedEntry;
                else
                    _productEntries.Add(updatedEntry);

                SuccessMessage = "Muvaffaqiyatli yangilandi!";
                CleanupAfterSave();
            }
            else ErrorMessage = response.Message ?? "Yangilashda xatolik!";
        }
        else
        {
            var response = await client.ProductEntries.Create(request).Handle(l => IsLoading = l);
            if (response.IsSuccess)
            {
                var newEntry = mapper.Map<ProductEntryViewModel>(CurrentProductEntry);
                newEntry.Id = response.Data;
                newEntry.ProductType = CurrentProduct.SelectedType!;
                newEntry.ProductType.Product = CurrentProduct;

                _productEntries.Insert(0, newEntry);
                SuccessMessage = "Muvaffaqiyatli saqlandi!";

                if (IsNewProductMode)
                    await LoadProductsAsync();

                CleanupAfterSave();
            }
            else ErrorMessage = response.Message ?? "Saqlashda xatolik!";
        }
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedProductEntry is null) return;

        if (IsEditing && !Confirm("Hozirgi tahrirlash jarayoni bekor qilinadi. Davom etasizmi?")) return;

        Cancel();

        IsEditing = true;
        IsNewProductMode = false;

        backupIndex = _productEntries.IndexOf(SelectedProductEntry);
        backupEntry = mapper.Map<ProductEntryViewModel>(SelectedProductEntry);

        CurrentProductEntry = mapper.Map<ProductEntryViewModel>(SelectedProductEntry);

        var product = AvailableProducts.FirstOrDefault(p => p.Id == SelectedProductEntry.ProductType!.ProductId);
        if (product is null && SelectedProductEntry.ProductType?.Product is not null)
            product = AvailableProducts.FirstOrDefault(p => p.Code == SelectedProductEntry.ProductType.Product.Code);

        if (product is not null)
        {
            CurrentProduct = product;
            ProductCode = CurrentProduct.Code;

            var type = CurrentProduct.ProductTypes.FirstOrDefault(pt => pt.Type == SelectedProductEntry.ProductType!.Type);
            if (type is not null)
                CurrentProduct.SelectedType = type;
            else
            {
                CurrentProduct.SelectedType = SelectedProductEntry.ProductType!;
                if (!CurrentProduct.ProductTypes.Any(pt => pt.Type == SelectedProductEntry.ProductType!.Type))
                    CurrentProduct.ProductTypes.Add(SelectedProductEntry.ProductType!);
            }
        }
        else
        {
            CurrentProduct = new ProductViewModel();
            ProductCode = string.Empty;
        }

        _productEntries.Remove(SelectedProductEntry);
        SelectedProductEntry = null;
    }

    [RelayCommand]
    private async Task Delete(ProductEntryViewModel? entry)
    {
        if (entry is null) return;

        if (!Confirm("Ushbu kirimni o'chirmoqchimisiz?")) return;

        var response = await client.ProductEntries.Delete(entry.Id).Handle(l => IsLoading = l);

        if (response.IsSuccess)
        {
            _productEntries.Remove(entry);
            SuccessMessage = "Muvaffaqiyatli o'chirildi!";
            _ = LoadProductSummaryAsync();
        }
        else
        {
            ErrorMessage = response.Message ?? "O'chirishda xatolik!";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsEditing && backupEntry is not null)
        {
            if (backupIndex >= 0 && backupIndex <= _productEntries.Count)
                _productEntries.Insert(backupIndex, backupEntry);
            else
                _productEntries.Add(backupEntry);
        }

        CleanupAfterSave();
    }

    #endregion

    #region Helpers

    private void CleanupAfterSave()
    {
        var lastDate = CurrentProductEntry.Date;
        IsEditing = false;
        IsNewProductMode = false;
        backupEntry = null;
        backupIndex = -1;
        CurrentProductEntry = new();
        CurrentProductEntry.Date = lastDate;
        CurrentProduct = new();
        ProductCode = string.Empty;
        _ = LoadProductSummaryAsync();
    }

    private bool Validate()
    {
        if (CurrentProduct is null) return SetWarning("Mahsulot tanlanmagan!");
        if (string.IsNullOrWhiteSpace(CurrentProduct.Code)) return SetWarning("Mahsulot kodi kiritilmagan!");
        if (CurrentProduct.SelectedType is null) return SetWarning("Mahsulot turi tanlanmagan!");

        if (IsNewProductMode)
        {
            if (string.IsNullOrWhiteSpace(CurrentProduct.Name)) return SetWarning("Mahsulot nomi kiritilmagan!");
        }

        if (string.IsNullOrWhiteSpace(CurrentProduct.SelectedType.Type)) return SetWarning("Razmer nomi kiritilmagan!");

        if (!CurrentProductEntry.Count.HasValue || CurrentProductEntry.Count <= 0) return SetWarning("Son (Count) kiritilmagan!");

        return true;
    }

    private bool SetWarning(string msg)
    {
        WarningMessage = msg;
        return false;
    }

    private void UpdateEntryCalculations()
    {
        if (CurrentProduct?.SelectedType is not null)
        {
            CurrentProductEntry.UnitPrice = CurrentProduct.SelectedType.UnitPrice;
            CurrentProductEntry.BundleItemCount = CurrentProduct.SelectedType.BundleItemCount;

            if (CurrentProductEntry.BundleCount.HasValue && CurrentProductEntry.BundleItemCount.HasValue)
                CurrentProductEntry.Count = CurrentProductEntry.BundleCount * CurrentProductEntry.BundleItemCount;
        }
    }

    private bool MatchesSearch(ProductEntryViewModel e)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var search = SearchText.Trim();
        return TransliterationHelper.ContainsIgnoreScript(e.ProductType?.Product?.Name ?? string.Empty, search)
            || TransliterationHelper.ContainsIgnoreScript(e.ProductType?.Product?.Code ?? string.Empty, search)
            || TransliterationHelper.ContainsIgnoreScript(e.ProductType?.Type ?? string.Empty, search);
    }

    #endregion Helpers
}
