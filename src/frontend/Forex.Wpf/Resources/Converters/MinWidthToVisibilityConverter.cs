namespace Forex.Wpf.Resources.Converters;

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

public sealed class MinWidthToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width)
            return Visibility.Visible;

        if (parameter is null)
            return Visibility.Visible;

        if (!double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var minWidth))
            return Visibility.Visible;

        return width >= minWidth ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
