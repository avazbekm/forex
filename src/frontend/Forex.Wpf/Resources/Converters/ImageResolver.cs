namespace Forex.Wpf.Resources.Converters;

using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public class ImageResolver : IValueConverter, IMultiValueConverter
{
    private const string MinioEndpoint = "http://localhost:9000";
    private const string MinioBucket = "forex-storage";

    private static readonly Lazy<ImageSource?> _placeholder = new(LoadPlaceholder);

    // ================================
    // IValueConverter - Simple Binding
    // ================================
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return _placeholder.Value ?? Binding.DoNothing;

        return LoadImage(path) ?? _placeholder.Value ?? Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    // ================================
    // IMultiValueConverter - Universal Components
    // ================================
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2 || values[0] is null || values[1] is null)
            return targetType == typeof(string) ? string.Empty : _placeholder.Value;

        var dataContext = values[0];
        var propertyName = values[1]?.ToString();

        if (string.IsNullOrWhiteSpace(propertyName))
            return targetType == typeof(string) ? string.Empty : _placeholder.Value;

        try
        {
            var property = dataContext.GetType().GetProperty(propertyName);
            var value = property?.GetValue(dataContext);

            if (value is null)
                return targetType == typeof(string) ? string.Empty : _placeholder.Value;

            if (targetType == typeof(string))
                return value.ToString() ?? string.Empty;

            if (value is string path && !string.IsNullOrWhiteSpace(path))
                return LoadImage(path) ?? _placeholder.Value;

            return _placeholder.Value;
        }
        catch
        {
            return targetType == typeof(string) ? string.Empty : _placeholder.Value;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    // ================================
    // Core Logic - Image yuklash
    // ================================

    private static ImageSource? LoadImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return _placeholder.Value;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

            if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                {
                    var cacheBuster = $"{path}{(path.Contains('?') ? '&' : '?')}nocache={DateTime.UtcNow.Ticks}";
                    bitmap.UriSource = new Uri(cacheBuster);
                    bitmap.EndInit();

                    if (!bitmap.IsDownloading)
                        bitmap.Freeze();

                    return bitmap;
                }
                else
                {
                    bitmap.UriSource = uri;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }

            if (File.Exists(path))
            {
                return LoadLocalFile(path);
            }

            if (path.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var fullUrl = $"{MinioEndpoint}/{MinioBucket}/{path}?nocache={DateTime.UtcNow.Ticks}";
                bitmap.UriSource = new Uri(fullUrl);
                bitmap.EndInit();

                if (!bitmap.IsDownloading)
                    bitmap.Freeze();

                return bitmap;
            }

            return _placeholder.Value;
        }
        catch
        {
            return _placeholder.Value;
        }
    }

    private static ImageSource? LoadLocalFile(string path)
    {
        try
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

                var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
        }
        catch
        {
            return _placeholder.Value;
        }
    }

    private static ImageSource? LoadPlaceholder()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Forex.Wpf;component/Resources/Assets/default.png");
            var bitmap = new BitmapImage(uri);
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
