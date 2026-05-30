namespace Forex.Wpf.Pages.Products.ViewModels;

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
using System.Windows.Data;

public partial class ProductPageViewModel : ViewModelBase
{
    private readonly ForexClient client;
    private readonly IMapper mapper;
    private ProductEntryViewModel? backupEntry;
    private int backupIndex = -1;
    private int currentPage = 1;
    private bool hasMoreItems = true;
    private bool _suppressFilter;
    private const int PageSize = 35;
    [ObservableProperty] private bool isNewProductMode;
    [ObservableProperty] private ProductViewModel currentProduct;
    [ObservableProperty] private DateTime? filterFromDate;
    [ObservableProperty] private DateTime? filterToDate;
    [ObservableProperty] private string searchText = string.Empty;

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
        await Task.WhenAll(
            LoadProductsAsync(),
            LoadProductEntriesAsync());
    }

    private async Task LoadProductsAsync()
    {
        var response = await client.Products.GetAllAsync().Handle(l => IsLoading = l);
        if (response.IsSuccess) AvailableProducts = mapper.Map<ObservableCollection<ProductViewModel>>(response.Data);
        else ErrorMessage = response.Message ?? "Mahsulotlarni yuklashda xatolik!";
    }

    private async Task LoadProductEntriesAsync()
    {
        FilteringRequest request = new()
        {
            Filters = new() { ["producttype"] = ["include:product"] },
            Descending = true,
            SortBy = "date",
            Page = currentPage,
            PageSize = PageSize
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

        var response = await client.ProductEntries.Filter(request).Handle(l => IsLoading = l);
        if (response.IsSuccess)
        {
            _productEntries.AddRange(mapper.Map<ObservableCollection<ProductEntryViewModel>>(response.Data));
            if (response.Data.Count < PageSize) hasMoreItems = false;
        }
        else ErrorMessage = response.Message ?? "Kirim tarixini yuklashda xatolik!";
    }

    [RelayCommand]
    private async Task LoadMoreEntries()
    {
        if (IsLoading || !hasMoreItems) return;

        currentPage++;
        await LoadProductEntriesAsync();
    }

    partial void OnFilterFromDateChanged(DateTime? value) => _ = ApplyDateFilter();
    partial void OnFilterToDateChanged(DateTime? value) => _ = ApplyDateFilter();
    partial void OnSearchTextChanged(string value) => ProductEntriesView.Refresh();

    [RelayCommand]
    private async Task ClearDateFilter()
    {
        _suppressFilter = true;
        FilterFromDate = null;
        FilterToDate = null;
        _suppressFilter = false;
        currentPage = 1;
        hasMoreItems = true;
        _productEntries.Clear();
        await LoadProductEntriesAsync();
    }

    private async Task ApplyDateFilter()
    {
        if (_suppressFilter) return;
        currentPage = 1;
        hasMoreItems = true;
        _productEntries.Clear();
        await LoadProductEntriesAsync();
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
        }
        else if (Confirm($"'{newValue}' yangi mahsulot sifatida qo'shilsinmi?"))
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
        IsEditing = false;
        IsNewProductMode = false;
        backupEntry = null;
        backupIndex = -1;
        CurrentProductEntry = new();
        CurrentProduct = new();
        ProductCode = string.Empty;
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
