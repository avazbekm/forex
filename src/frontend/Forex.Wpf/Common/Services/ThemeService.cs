namespace Forex.Wpf.Common.Services;

using System.Windows;

public static class ThemeService
{
    public static void Apply(bool dark)
    {
        var app = Application.Current;
        if (app is null) return;

        try
        {
            var dicts = app.Resources.MergedDictionaries;

            // Eski custom mavzuni olib tashlaymiz
            for (int i = dicts.Count - 1; i >= 0; i--)
            {
                var src = dicts[i].Source?.OriginalString ?? string.Empty;
                if (src.Contains("LightTheme.xaml") || src.Contains("DarkTheme.xaml"))
                    dicts.RemoveAt(i);
            }

            // Custom mavzuni ENG OXIRIGA qo'shamiz — shunda u MaterialDesign
            // ranglarini (MaterialDesignPaper, MaterialDesignBody, ...) override qiladi.
            var uri = new Uri(dark ? "/Resources/Themes/DarkTheme.xaml" : "/Resources/Themes/LightTheme.xaml", UriKind.Relative);
            dicts.Add(new ResourceDictionary { Source = uri });
        }
        catch
        {
        }
    }
}
