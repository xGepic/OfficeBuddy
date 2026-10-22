using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;

namespace OfficeBuddy.Core.Images;

/// <summary>
/// An image as pixels in memory, the right way up, that other images can be drawn onto: blue,
/// green, red and alpha bytes per pixel, row by row, with the colours already multiplied by alpha.
/// </summary>
internal sealed class ImageCanvas(int width, int height, byte[] pixels)
{
    /// <summary>The most pixels an image the app writes may have; bigger ones would hardly fit in memory.</summary>
    public const long MaxPixels = 64_000_000;

    private readonly byte[] _pixels = pixels;

    public int Width { get; } = width;

    public int Height { get; } = height;

    /// <summary>A canvas of the given size in plain white.</summary>
    public static ImageCanvas White(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        Array.Fill(pixels, (byte)255);
        return new ImageCanvas(width, height, pixels);
    }

    /// <summary>
    /// Draws <paramref name="picture"/> with its top left corner at <paramref name="left"/>,
    /// <paramref name="top"/>. Where it is transparent, this canvas shows through; whatever falls
    /// outside this canvas is cut off.
    /// </summary>
    public void Draw(ImageCanvas picture, int left, int top)
    {
        var (fromX, toX) = (Math.Max(0, -left), Math.Min(picture.Width, Width - left));
        var (fromY, toY) = (Math.Max(0, -top), Math.Min(picture.Height, Height - top));
        for (var y = fromY; y < toY; y++)
        {
            var source = (y * picture.Width + fromX) * 4;
            var target = ((top + y) * Width + left + fromX) * 4;
            for (var x = fromX; x < toX; x++, source += 4, target += 4)
            {
                var alpha = picture._pixels[source + 3];
                if (alpha == 255)
                {
                    Buffer.BlockCopy(picture._pixels, source, _pixels, target, 4);
                    continue;
                }

                // Premultiplied "over": the picture's colour plus what shows through its transparency.
                var through = 255 - alpha;
                for (var channel = 0; channel < 4; channel++)
                {
                    _pixels[target + channel] =
                        (byte)(picture._pixels[source + channel] + (_pixels[target + channel] * through + 127) / 255);
                }
            }
        }
    }

    /// <summary>
    /// Makes light, paper-coloured pixels transparent and the others more or less see-through by how
    /// dark they are, so a signature scanned on white paper lies on a Page like ink. The ink keeps
    /// its colour.
    /// </summary>
    public void ClearPaper()
    {
        // Brightness at and above which a pixel counts as paper, and at and below which as ink.
        const double Paper = 230;
        const double Ink = 160;
        for (var i = 0; i < _pixels.Length; i += 4)
        {
            var alpha = _pixels[i + 3];
            if (alpha == 0)
            {
                continue;
            }

            // Brightness as the eye sees it, of the colour before it was multiplied by alpha.
            var brightness = (0.114 * _pixels[i] + 0.587 * _pixels[i + 1] + 0.299 * _pixels[i + 2]) * 255 / alpha;
            var ink = Math.Clamp((Paper - brightness) / (Paper - Ink), 0, 1);
            for (var channel = 0; channel < 4; channel++)
            {
                _pixels[i + channel] = (byte)Math.Round(_pixels[i + channel] * ink);
            }
        }
    }

    /// <summary>The canvas as a PNG file, keeping its transparency, or as a JPG file.</summary>
    public async Task<byte[]> EncodeAsync(bool asPng)
    {
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(_pixels.AsBuffer(), BitmapPixelFormat.Bgra8, Width, Height,
            asPng ? BitmapAlphaMode.Premultiplied : BitmapAlphaMode.Ignore);
        return asPng ? await ImageFile.EncodePngAsync(bitmap) : await ImageFile.EncodeJpegAsync(bitmap);
    }
}
