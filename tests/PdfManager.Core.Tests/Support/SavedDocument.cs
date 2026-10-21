using System.Runtime.InteropServices.WindowsRuntime;
using UglyToad.PdfPig;
using WindowsPdfDocument = Windows.Data.Pdf.PdfDocument;
using WindowsPdfPageRenderOptions = Windows.Data.Pdf.PdfPageRenderOptions;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PdfManager.Core.Tests.Support;

/// <summary>A Page as found in a saved Document: its label text and its rotation.</summary>
public sealed record SavedPage(string Label, int Rotation);

/// <summary>
/// Reads a saved Document back independently of the library that wrote it: its content with PdfPig,
/// what it looks like with the PDF renderer built into Windows.
/// </summary>
public static class SavedDocument
{
    public static IReadOnlyList<SavedPage> ReadPages(string path)
    {
        using var document = PdfDocument.Open(path);
        return [.. document.GetPages().Select(page => new SavedPage(page.Text.Trim(), page.Rotation.Value))];
    }

    public static IReadOnlyList<string> ReadLabels(string path) =>
        [.. ReadPages(path).Select(page => page.Label)];

    /// <summary>The size of every Page, in points.</summary>
    public static IReadOnlyList<(double Width, double Height)> ReadPageSizes(string path)
    {
        using var document = PdfDocument.Open(path);
        return [.. document.GetPages().Select(page => (page.Width, page.Height))];
    }

    /// <summary>The resolution of every image on every Page, in pixels per inch across the Page.</summary>
    public static IReadOnlyList<double> ReadImageResolutions(string path)
    {
        using var document = PdfDocument.Open(path);
        return [.. document.GetPages()
            .SelectMany(page => page.GetImages())
            .Select(image => image.WidthInSamples / (image.BoundingBox.Width / 72))];
    }

    /// <summary>A Page as Windows draws it, with its rotation applied.</summary>
    public static async Task<RenderedPage> RenderAsync(string path, int pageIndex)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        var pdf = await WindowsPdfDocument.LoadFromFileAsync(file);
        using var page = pdf.GetPage((uint)pageIndex);
        using var stream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream, new WindowsPdfPageRenderOptions { DestinationWidth = 600 });
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var pixels = (await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
            new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage))
            .DetachPixelData();
        return new RenderedPage(pixels, (int)decoder.PixelWidth, (int)decoder.PixelHeight);
    }
}

/// <summary>A drawn Page: its size in pixels and its colours, as BGRA.</summary>
public sealed record RenderedPage(byte[] Pixels, int Width, int Height)
{
    public bool IsLandscape => Width > Height;

    /// <summary>
    /// The colour at a point given as a share of the width and height, averaged over a small patch
    /// so scanner grain and JPEG noise don't matter.
    /// </summary>
    public (int R, int G, int B) ColourAt((double X, double Y) at)
    {
        var (centreX, centreY) = ((int)(Width * at.X), (int)(Height * at.Y));
        var (r, g, b, count) = (0, 0, 0, 0);
        for (var y = Math.Max(centreY - 5, 0); y <= Math.Min(centreY + 5, Height - 1); y++)
        {
            for (var x = Math.Max(centreX - 5, 0); x <= Math.Min(centreX + 5, Width - 1); x++)
            {
                var i = (y * Width + x) * 4;
                (b, g, r, count) = (b + Pixels[i], g + Pixels[i + 1], r + Pixels[i + 2], count + 1);
            }
        }
        return (r / count, g / count, b / count);
    }
}
