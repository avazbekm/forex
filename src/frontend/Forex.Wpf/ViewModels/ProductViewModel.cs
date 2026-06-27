namespace Forex.Wpf.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forex.ClientService.Enums;
using Forex.Wpf.Pages.Common;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;

public partial class ProductViewModel : ViewModelBase
{
    public long Id { get; set; }
    public long UnitMeasureId { get; set; }
    [ObservableProperty] private string code = string.Empty;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private ProductionOrigin productionOrigin;
    [ObservableProperty] private UnitMeasuerViewModel unitMeasure = default!;
    [ObservableProperty] private string imagePath = string.Empty;
    [ObservableProperty] private string imagePreviewPath = string.Empty;

    // UI'da ko'rsatish uchun - preview priority
    public string DisplayImagePath => !string.IsNullOrWhiteSpace(ImagePreviewPath) ? ImagePreviewPath : ImagePath;

    partial void OnImagePathChanged(string value) => OnPropertyChanged(nameof(DisplayImagePath));
    partial void OnImagePreviewPathChanged(string value) => OnPropertyChanged(nameof(DisplayImagePath));

    [ObservableProperty] private ObservableCollection<ProductTypeViewModel> productTypes = [];
    [ObservableProperty] private ProductTypeViewModel selectedType = new();
    private ProductViewModel? selected;

    #region Commands

    [RelayCommand]
    private async Task SelectImageAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Rasmlar (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Title = "Mahsulot rasmi tanlash"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            IsLoading = true;
            var selectedFilePath = dialog.FileName;

            using var fileStream = File.OpenRead(selectedFilePath);
            using var compressedStream = await Forex.Wpf.Services.ImageCompressionService.CompressImageAsync(fileStream);

            var tempFileName = $"temp_{Guid.NewGuid():N}.jpg";
            var tempPath = Path.Combine(Path.GetTempPath(), tempFileName);

            using (var tempFile = File.Create(tempPath))
            {
                await compressedStream.CopyToAsync(tempFile);
            }

            ImagePath = tempPath;
            ImagePreviewPath = tempPath;
        }
        catch
        {
            ErrorMessage = "Rasmni yuklashda xatolik!";
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion Commands

    #region Property Changes

    public ProductViewModel? Selected
    {
        get => selected;
        set
        {
            if (SetProperty(ref selected, value) && value is not null)
            {
                Id = value.Id;
                Code = value.Code;
                Name = value.Name;
                UnitMeasure = value.UnitMeasure;
                SelectedType = value.SelectedType;
                ProductTypes = new ObservableCollection<ProductTypeViewModel>(value.ProductTypes ?? []);
            }
        }
    }

    #endregion Property Changes
}