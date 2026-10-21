using Windows.Graphics.Imaging;
using Windows.Storage;

namespace PdfManager.Core.Tests.Support;

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
}

public sealed record Edges(double Top, double Right, double Bottom, double Left);
