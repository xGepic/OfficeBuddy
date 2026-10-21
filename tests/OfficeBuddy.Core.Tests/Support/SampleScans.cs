using System.Runtime.InteropServices.WindowsRuntime;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace OfficeBuddy.Core.Tests.Support;

public static partial class SamplePdfs
{
    /// <summary>Where the red logo sits on every Page of a colour scan, as a share of the Page's width and height.</summary>
    public static readonly (double X, double Y) ColourScanLogo = (0.2, 0.1);

    /// <summary>
    /// A colour scan of <paramref name="pageCount"/> A4 letters at 300 dpi, saved as high-quality JPEGs
    /// the way a scanner does: slightly grainy paper, a red logo (at <see cref="ColourScanLogo"/>), a
    /// coloured photo, lines of dark text and a blue stamp. The Pages differ in their grain, like
    /// real sheets. About 3 MB per Page.
    /// </summary>
    public static Task<string> CreateColourScanAsync(string folder, string fileName, int pageCount) =>
        CreateScanAsync(folder, fileName, pageCount, async i =>
            new ScannedPage(await EncodeJpegAsync(DrawScannedLetter(seed: i), ScanWidth, ScanHeight), A4, Label: null));

    /// <summary>
    /// A scan of <paramref name="pageCount"/> A4 Pages that is already at a low resolution (90 dpi)
    /// and full of fine colour detail, like photos of gravel: Compress cannot make it much smaller.
    /// Just under 1 MB per Page. Every Page carries a label, F1, F2, … Page
    /// <paramref name="posterPageNumber"/>, if given, is a 1 × 1 m poster of about 15 MB instead.
    /// </summary>
    public static Task<string> CreateDetailedLowResolutionScanAsync(
        string folder, string fileName, int pageCount, int? posterPageNumber = null)
    {
        var random = new Random(815);
        return CreateScanAsync(folder, fileName, pageCount, async i =>
        {
            var isPoster = i + 1 == posterPageNumber;
            var size = isPoster ? new XSize(XUnit.FromMillimeter(1000).Point, XUnit.FromMillimeter(1000).Point) : A4;
            // 90 pixels per inch of the Page.
            var (width, height) = ((int)(size.Width / 72 * 90), (int)(size.Height / 72 * 90));
            var pixels = new byte[width * height * 4];
            random.NextBytes(pixels);
            return new ScannedPage(await EncodeJpegAsync(pixels, width, height), size, $"F{i + 1}");
        });
    }

    private static readonly XSize A4 = new(XUnit.FromMillimeter(210).Point, XUnit.FromMillimeter(297).Point);

    /// <summary>One scanned Page: its image, the Page's size, and a label to tell it apart, if any.</summary>
    private sealed record ScannedPage(byte[] Jpeg, XSize Size, string? Label);

    /// <summary>A Document of Pages, each filled by one JPEG image the way a scanner writes them.</summary>
    private static async Task<string> CreateScanAsync(
        string folder, string fileName, int pageCount, Func<int, Task<ScannedPage>> scanPage)
    {
        using var document = new PdfDocument();
        for (var i = 0; i < pageCount; i++)
        {
            var scanned = await scanPage(i);
            var page = document.AddPage();
            (page.Width, page.Height) = (XUnit.FromPoint(scanned.Size.Width), XUnit.FromPoint(scanned.Size.Height));
            using var graphics = XGraphics.FromPdfPage(page);
            using var image = XImage.FromStream(new MemoryStream(scanned.Jpeg));
            graphics.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
            if (scanned.Label is not null)
            {
                DrawLabel(graphics, scanned.Label);
            }
        }

        var path = Path.Combine(folder, fileName);
        document.Save(path);
        return path;
    }

    // A4 at 300 dpi.
    private const int ScanWidth = 2480;
    private const int ScanHeight = 3508;

    /// <summary>The pixels of one scanned letter, as BGRA.</summary>
    private static byte[] DrawScannedLetter(int seed)
    {
        var random = new Random(seed);
        var grain = new byte[ScanWidth * ScanHeight];
        random.NextBytes(grain);
        var pixels = new byte[ScanWidth * ScanHeight * 4];

        void Fill(int left, int top, int width, int height, Func<int, int, (int R, int G, int B)> colour)
        {
            for (var y = top; y < top + height; y++)
            {
                for (var x = left; x < left + width; x++)
                {
                    var (r, g, b) = colour(x - left, y - top);
                    // Scanner grain: up to 8 levels either way.
                    var noise = grain[y * ScanWidth + x] % 17 - 8;
                    var i = (y * ScanWidth + x) * 4;
                    pixels[i] = (byte)Math.Clamp(b + noise, 0, 255);
                    pixels[i + 1] = (byte)Math.Clamp(g + noise, 0, 255);
                    pixels[i + 2] = (byte)Math.Clamp(r + noise, 0, 255);
                    pixels[i + 3] = 255;
                }
            }
        }

        Fill(0, 0, ScanWidth, ScanHeight, (_, _) => (246, 244, 236));
        var (logoX, logoY) = ((int)(ScanWidth * ColourScanLogo.X), (int)(ScanHeight * ColourScanLogo.Y));
        Fill(logoX - 250, logoY - 120, 500, 240, (_, _) => (200, 30, 40));
        Fill(300, 1100, 1880, 1000, (x, y) => (60 + x * 150 / 1880, 120 + y * 100 / 1000, 200 - x * 120 / 1880));
        for (var line = 0; line < 22; line++)
        {
            Fill(300, 2250 + line * 50, 900 + random.Next(980), 28, (_, _) => (40, 40, 45));
        }
        Fill(1700, 3200, 300, 200, (_, _) => (40, 70, 170));
        return pixels;
    }

    private static async Task<byte[]> EncodeJpegAsync(byte[] bgra, int width, int height)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream,
            [new("ImageQuality", new BitmapTypedValue(0.95f, Windows.Foundation.PropertyType.Single))]);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)width, (uint)height, 300, 300, bgra);
        await encoder.FlushAsync();

        var jpeg = new byte[stream.Size];
        stream.Seek(0);
        await stream.ReadAsync(jpeg.AsBuffer(), (uint)jpeg.Length, InputStreamOptions.None);
        return jpeg;
    }
}
