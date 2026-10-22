using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace OfficeBuddy.Core;

/// <summary>Draws the Pages of one Document as images, with the PDF renderer built into Windows.</summary>
internal sealed class PageRenderer
{
    private readonly PdfDocument _pdf;
    // Windows reads the Document from this stream while rendering, so it lives as long as the renderer.
    private readonly InMemoryRandomAccessStream _source;
    // One Page at a time: thumbnails then appear one after another instead of all at the end.
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);
    // Measured on the first render: how many pixels Windows draws per device-independent pixel.
    private double? _pixelsPerDip;

    // A bigger image (e.g. a large plan at 200 dpi) would not fit in memory; such Pages are scaled down to this.
    private const double MaxPixels = 64_000_000;

    private PageRenderer(PdfDocument pdf, InMemoryRandomAccessStream source)
    {
        _pdf = pdf;
        _source = source;
    }

    public static async Task<PageRenderer> OpenAsync(byte[] content)
    {
        var source = new InMemoryRandomAccessStream();
        await source.WriteAsync(content.AsBuffer());
        source.Seek(0);
        return new PageRenderer(await PdfDocument.LoadFromStreamAsync(source), source);
    }

    /// <summary>
    /// Renders a Page as a PNG image that fits inside the given pixel size, keeping its proportions
    /// and with the Page's own rotation applied.
    /// </summary>
    public Task<byte[]> RenderPngAsync(int pageIndex, int maxWidth, int maxHeight) =>
        RenderAsync(pageIndex, BitmapEncoder.PngEncoderId, size =>
        {
            var scale = Math.Min(maxWidth / size.Width, maxHeight / size.Height);
            return (size.Width * scale, size.Height * scale);
        });

    /// <summary>
    /// Renders a Page as a JPG image at its real size and the given resolution, with its own
    /// rotation applied. A Page too big for that is scaled down to fit in memory.
    /// </summary>
    public Task<byte[]> RenderJpegAsync(int pageIndex, int dpi) =>
        // Windows gives the Page size in device-independent pixels, 96 to the inch.
        RenderAsync(pageIndex, BitmapEncoder.JpegEncoderId, size =>
        {
            var (width, height) = (size.Width * dpi / 96, size.Height * dpi / 96);
            if (width * height <= MaxPixels)
            {
                return (width, height);
            }

            // Rounded down, so the scaled image stays under the limit.
            var scale = Math.Sqrt(MaxPixels / (width * height));
            return (Math.Floor(width * scale), Math.Floor(height * scale));
        });

    /// <param name="pixelSize">The image size in pixels for the Page's size (which already takes its rotation into account).</param>
    private async Task<byte[]> RenderAsync(
        int pageIndex, Guid encoderId, Func<Windows.Foundation.Size, (double Width, double Height)> pixelSize)
    {
        await _oneAtATime.WaitAsync();
        try
        {
            using var page = _pdf.GetPage((uint)pageIndex);
            var (exactWidth, exactHeight) = pixelSize(page.Size);
            var width = (uint)Math.Max(1, Math.Round(exactWidth));
            var height = (uint)Math.Max(1, Math.Round(exactHeight));

            // Windows takes the size in device-independent pixels and scales it by the display
            // setting (e.g. 150 %). Ask for correspondingly less, then fix any rounding difference.
            var pixelsPerDip = _pixelsPerDip ??= await MeasurePixelsPerDipAsync(page);
            using var image = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(image, new PdfPageRenderOptions
            {
                DestinationWidth = (uint)Math.Max(1, Math.Round(width / pixelsPerDip)),
                DestinationHeight = (uint)Math.Max(1, Math.Round(height / pixelsPerDip)),
                // Lossless first, so a JPG is compressed only once, below.
                BitmapEncoderId = BitmapEncoder.PngEncoderId,
            });

            var decoder = await BitmapDecoder.CreateAsync(image);
            var exactSize = decoder.PixelWidth == width && decoder.PixelHeight == height;
            if (exactSize && encoderId == BitmapEncoder.PngEncoderId)
            {
                return await ReadAllAsync(image);
            }

            using var result = new InMemoryRandomAccessStream();
            var encoder = encoderId == BitmapEncoder.JpegEncoderId
                ? await BitmapEncoder.CreateAsync(encoderId, result,
                    [new("ImageQuality", new BitmapTypedValue(JpegQuality, Windows.Foundation.PropertyType.Single))])
                : await BitmapEncoder.CreateAsync(encoderId, result);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            encoder.SetSoftwareBitmap(bitmap);
            if (!exactSize)
            {
                encoder.BitmapTransform.ScaledWidth = width;
                encoder.BitmapTransform.ScaledHeight = height;
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
            }
            await encoder.FlushAsync();
            return await ReadAllAsync(result);
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    // High enough that small print stays crisp.
    private const float JpegQuality = 0.92f;

    /// <summary>Renders a tiny image once to see how many pixels Windows draws per device-independent pixel.</summary>
    private static async Task<double> MeasurePixelsPerDipAsync(PdfPage page)
    {
        const uint ProbeSize = 64;
        using var probe = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(probe, new PdfPageRenderOptions
        {
            DestinationWidth = ProbeSize,
            DestinationHeight = ProbeSize,
            BitmapEncoderId = BitmapEncoder.PngEncoderId,
        });
        var decoder = await BitmapDecoder.CreateAsync(probe);
        return (double)decoder.PixelWidth / ProbeSize;
    }

    private static async Task<byte[]> ReadAllAsync(InMemoryRandomAccessStream stream)
    {
        stream.Seek(0);
        var buffer = new byte[stream.Size];
        await stream.ReadAsync(buffer.AsBuffer(), (uint)buffer.Length, InputStreamOptions.None);
        return buffer;
    }
}
