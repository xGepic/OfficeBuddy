using Windows.Graphics.Imaging;
using Windows.Storage;

namespace OfficeBuddy.Core.Tests.Support;

/// <summary>
/// What an image file on disk turned out to be, read with the Windows image decoder, including how
/// dark a strip along each edge is (0 white to 255 black).
/// </summary>
public sealed record SavedImage(bool IsJpeg, int Width, int Height, Edges Darkness)
{
    public static async Task<SavedImage> ReadAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var (width, height) = ((int)decoder.PixelWidth, (int)decoder.PixelHeight);
        var pixels = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
            new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage))
            .DetachPixelData();

        double Darkness(int left, int top, int right, int bottom)
        {
            long sum = 0;
            for (var y = top; y < bottom; y++)
            {
                for (var x = left; x < right; x++)
                {
                    var i = (y * width + x) * 4;
                    sum += 255 - (pixels[i] + pixels[i + 1] + pixels[i + 2]) / 3;
                }
            }
            return (double)sum / ((right - left) * (bottom - top));
        }

        var (stripX, stripY) = (width / 20, height / 20);
        var edges = new Edges(
            Top: Darkness(0, 0, width, stripY),
            Right: Darkness(width - stripX, 0, width, height),
            Bottom: Darkness(0, height - stripY, width, height),
            Left: Darkness(0, 0, stripX, height));
        return new SavedImage(decoder.DecoderInformation.CodecId == BitmapDecoder.JpegDecoderId, width, height, edges);
    }

    /// <summary>
    /// The colour of the pixel at <paramref name="x"/>, <paramref name="y"/>, given as shares of the
    /// image's width and height, as it shows on a white background.
    /// </summary>
    public static async Task<(int R, int G, int B)> ColourAtAsync(string path, double x, double y)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var width = (int)decoder.PixelWidth;
        var pixels = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage))
            .DetachPixelData();

        var i = ((int)(y * decoder.PixelHeight) * width + (int)(x * width)) * 4;
        var white = 255 - pixels[i + 3];
        return (pixels[i + 2] + white, pixels[i + 1] + white, pixels[i] + white);
    }

    /// <summary>Whether two colours look alike, allowing for what JPG compression changes.</summary>
    public static bool AreAlike((int R, int G, int B) actual, (int R, int G, int B) expected) =>
        Math.Abs(actual.R - expected.R) <= 40 && Math.Abs(actual.G - expected.G) <= 40 && Math.Abs(actual.B - expected.B) <= 40;
}

public sealed record Edges(double Top, double Right, double Bottom, double Left);
