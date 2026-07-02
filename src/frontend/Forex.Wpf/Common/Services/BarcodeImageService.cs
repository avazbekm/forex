namespace Forex.Wpf.Common.Services;

using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

public static class BarcodeImageService
{
    public static BitmapSource Render(string code, int width = 360, int height = 120, BarcodeFormat format = BarcodeFormat.CODE_128)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = format,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 2,
                PureBarcode = true
            }
        };

        var pixelData = writer.Write(code);

        var bitmap = BitmapSource.Create(
            pixelData.Width, pixelData.Height, 96, 96,
            PixelFormats.Bgra32, null,
            pixelData.Pixels, pixelData.Width * 4);

        bitmap.Freeze();
        return bitmap;
    }
}
