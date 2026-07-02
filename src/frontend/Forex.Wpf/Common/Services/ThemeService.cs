namespace Forex.Wpf.Common.Services;

using MaterialDesignThemes.Wpf;
using System.Windows;

public static class ThemeService
{
    public static void Apply(bool dark)
    {
        var app = Application.Current;
        if (app is null) return;

        try
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(theme);

            var dicts = app.Resources.MergedDictionaries;

            for (int i = dicts.Count - 1; i >= 0; i--)
            {
                var src = dicts[i].Source?.OriginalString ?? string.Empty;
                if (src.Contains("LightTheme.xaml") || src.Contains("DarkTheme.xaml"))
                    dicts.RemoveAt(i);
            }

            var uri = new Uri(dark ? "/Resources/Themes/DarkTheme.xaml" : "/Resources/Themes/LightTheme.xaml", UriKind.Relative);
            dicts.Add(new ResourceDictionary { Source = uri });
        }
        catch
        {
        }
    }
}
