using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PdfManager.Core;

/// <summary>How far Blank page detection has got: <paramref name="Checked"/> of <paramref name="Total"/> Pages.</summary>
public readonly record struct BlankPageProgress(int Checked, int Total);

/// <summary>
/// Finds Blank pages: Pages with essentially no ink apart from scanner noise, such as the empty
/// backs of a duplex scan. Cautious on purpose: a Page with only a signature, a stamp or a faint
/// pencil note is not blank.
/// </summary>
internal static class BlankPageDetection
{
    // Long side of the rendered Page: about 20 pixels per cm on A4, so a thin pencil line shows up.
    private const int RenderSize = 600;

    // Scanners can leave dark bands right at the edges of the paper; only those few millimetres are ignored.
    private const double IgnoredBorder = 0.03;

    // Ink is this much darker than the paper, whatever colour the paper is. Faint pencil is just above it.
    private const int InkContrast = 24;

    // A mark smaller than this across (about 3 mm) is a speck of dust, not writing.
    private const int MinMarkSize = 6;

    // Blank only if the marks add up to fewer pixels than this. A faint pencil note has about five times as many.
    private const int MaxInkPixels = 20;

    public static async Task<IReadOnlyList<int>> FindAsync(Document document, IProgress<BlankPageProgress>? progress)
    {
        var blankIndexes = new List<int>();
        for (var index = 0; index < document.PageCount; index++)
        {
            if (await IsBlankAsync(document, index))
            {
                blankIndexes.Add(index);
            }
            progress?.Report(new BlankPageProgress(index + 1, document.PageCount));
        }
        return blankIndexes;
    }

    private static async Task<bool> IsBlankAsync(Document document, int pageIndex)
    {
        try
        {
            var png = await document.RenderPageAsPngAsync(pageIndex, RenderSize, RenderSize);
            var image = await ReadBrightnessAsync(png);
            return CountInkInMarks(image) < MaxInkPixels;
        }
        catch (Exception)
        {
            // A Page that can't be looked at is kept.
            return false;
        }
    }

    /// <summary>
    /// Counts the ink pixels that belong to marks at least <see cref="MinMarkSize"/> across, inside
    /// the ignored border.
    /// </summary>
    private static int CountInkInMarks(Brightness image)
    {
        var (width, height) = (image.Width, image.Height);
        var (left, top) = ((int)(width * IgnoredBorder), (int)(height * IgnoredBorder));
        var (right, bottom) = (width - left, height - top);
        if (right <= left || bottom <= top)
        {
            return 0;
        }

        var inkBelow = PaperBrightness(image, left, top, right, bottom) - InkContrast;
        bool IsInk(int x, int y) =>
            x >= left && x < right && y >= top && y < bottom && image.Pixels[y * width + x] < inkBelow;

        // Walk each connected mark once, measuring its size and how many pixels it has.
        var visited = new bool[width * height];
        var pending = new Stack<(int X, int Y)>();
        var inkInMarks = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                if (visited[y * width + x] || !IsInk(x, y))
                {
                    continue;
                }

                visited[y * width + x] = true;
                pending.Push((x, y));
                var (pixels, minX, maxX, minY, maxY) = (0, x, x, y, y);
                while (pending.TryPop(out var pixel))
                {
                    pixels++;
                    (minX, maxX) = (Math.Min(minX, pixel.X), Math.Max(maxX, pixel.X));
                    (minY, maxY) = (Math.Min(minY, pixel.Y), Math.Max(maxY, pixel.Y));
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var (nx, ny) = (pixel.X + dx, pixel.Y + dy);
                            if (IsInk(nx, ny) && !visited[ny * width + nx])
                            {
                                visited[ny * width + nx] = true;
                                pending.Push((nx, ny));
                            }
                        }
                    }
                }

                if (Math.Max(maxX - minX, maxY - minY) + 1 >= MinMarkSize)
                {
                    inkInMarks += pixels;
                }
            }
        }
        return inkInMarks;
    }

    /// <summary>The median brightness of the area, which on a nearly empty Page is the paper itself.</summary>
    private static int PaperBrightness(Brightness image, int left, int top, int right, int bottom)
    {
        var histogram = new int[256];
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                histogram[image.Pixels[y * image.Width + x]]++;
            }
        }

        var half = (right - left) * (bottom - top) / 2;
        var seen = 0;
        for (var level = 0; level < 256; level++)
        {
            seen += histogram[level];
            if (seen > half)
            {
                return level;
            }
        }
        return 255;
    }

    /// <summary>The brightness of every pixel (0 black to 255 white), row by row.</summary>
    private sealed record Brightness(byte[] Pixels, int Width, int Height);

    private static async Task<Brightness> ReadBrightnessAsync(byte[] png)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(png.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var pixels = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        var bgra = pixels.DetachPixelData();

        var brightness = new byte[bgra.Length / 4];
        for (var i = 0; i < brightness.Length; i++)
        {
            var (blue, green, red) = (bgra[i * 4], bgra[i * 4 + 1], bgra[i * 4 + 2]);
            brightness[i] = (byte)((red * 299 + green * 587 + blue * 114) / 1000);
        }
        return new Brightness(brightness, (int)decoder.PixelWidth, (int)decoder.PixelHeight);
    }
}
