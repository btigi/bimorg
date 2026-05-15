using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Bimorg.Converters;

[ValueConversion(typeof(byte[]), typeof(ImageSource))]
public sealed class BytesToImageConverter : IValueConverter
{
    // Byte array to thumbnail
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] bytes || bytes.Length == 0)
            return null;

        try
        {
            // Copy so we never hold a reference that could be mutated or tied to transient buffers.
            var buffer = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);

            using var ms = new MemoryStream(buffer, writable: false);
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}