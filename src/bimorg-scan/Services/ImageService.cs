using System.IO;
using System.Windows.Media.Imaging;

namespace Bimorg.Scan.Services;

public sealed class ImageService
{
    public ImageFileInfo ReadInfo(string absolutePath)
    {
        absolutePath = Path.GetFullPath(absolutePath);
        var fi = new FileInfo(absolutePath);
        if (!fi.Exists)
            throw new FileNotFoundException("Image file not found.", absolutePath);

        using var fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(
            fs,
            BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.None);

        var frame = decoder.Frames.FirstOrDefault()
            ?? throw new InvalidOperationException("No decoded frame for: " + absolutePath);

        return new ImageFileInfo(
            frame.PixelWidth,
            frame.PixelHeight,
            fi.Length);
    }

    /// <summary>Encode a proportional JPEG thumbnail; longest edge is at most <paramref name="maxEdge"/> px.</summary>
    public byte[] CreateThumbnail(string absolutePath, int maxEdge)
    {
        if (maxEdge < 64)
            maxEdge = 64;

        absolutePath = Path.GetFullPath(absolutePath);

        int decodeW;
        int decodeH;
        {
            using var fsPeek = File.OpenRead(absolutePath);
            var dec = BitmapDecoder.Create(fsPeek, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
            var f = dec.Frames.First();
            var pw = f.PixelWidth;
            var ph = f.PixelHeight;
            if (pw >= ph)
            {
                decodeW = maxEdge;
                decodeH = 0;
            }
            else
            {
                decodeW = 0;
                decodeH = maxEdge;
            }
        }

        using var fsLoad = File.OpenRead(absolutePath);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = fsLoad;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = decodeW;
        bitmap.DecodePixelHeight = decodeH;
        bitmap.EndInit();
        bitmap.Freeze();

        var encoder = new JpegBitmapEncoder { QualityLevel = 80 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}

public readonly record struct ImageFileInfo(int Width, int Height, long FileSizeBytes);
