namespace Forex.Wpf.Common.Services;

using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Text.Json;

public partial class AppPreferences : ObservableObject
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ForexApp");
    private static readonly string FilePath = Path.Combine(Dir, "ui-prefs.json");

    public static AppPreferences Instance { get; } = Load();

    private bool loaded;

    [ObservableProperty] private bool showDashboardStats = true;
    [ObservableProperty] private bool showSalesTrend = true;
    [ObservableProperty] private bool showTopCustomers = true;
    [ObservableProperty] private bool showTopProducts = true;
    [ObservableProperty] private bool darkTheme;

    public bool AnyDashboardVisible =>
        ShowDashboardStats || ShowSalesTrend || ShowTopCustomers || ShowTopProducts;

    partial void OnShowDashboardStatsChanged(bool value) { Save(); OnPropertyChanged(nameof(AnyDashboardVisible)); }
    partial void OnShowSalesTrendChanged(bool value) { Save(); OnPropertyChanged(nameof(AnyDashboardVisible)); }
    partial void OnShowTopCustomersChanged(bool value) { Save(); OnPropertyChanged(nameof(AnyDashboardVisible)); }
    partial void OnShowTopProductsChanged(bool value) { Save(); OnPropertyChanged(nameof(AnyDashboardVisible)); }

    partial void OnDarkThemeChanged(bool value)
    {
        if (loaded) ThemeService.Apply(value);
        Save();
    }

    private sealed record Dto(bool ShowDashboardStats, bool ShowSalesTrend, bool ShowTopCustomers, bool ShowTopProducts, bool DarkTheme);

    private static AppPreferences Load()
    {
        var prefs = new AppPreferences();
        try
        {
            if (File.Exists(FilePath))
            {
                var dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(FilePath));
                if (dto is not null)
                {
                    prefs.ShowDashboardStats = dto.ShowDashboardStats;
                    prefs.ShowSalesTrend = dto.ShowSalesTrend;
                    prefs.ShowTopCustomers = dto.ShowTopCustomers;
                    prefs.ShowTopProducts = dto.ShowTopProducts;
                    prefs.DarkTheme = dto.DarkTheme;
                }
            }
        }
        catch
        {
        }
        prefs.loaded = true;
        return prefs;
    }

    private void Save()
    {
        if (!loaded) return;
        try
        {
            Directory.CreateDirectory(Dir);
            var dto = new Dto(ShowDashboardStats, ShowSalesTrend, ShowTopCustomers, ShowTopProducts, DarkTheme);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }
}
