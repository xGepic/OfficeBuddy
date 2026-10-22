using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace OfficeBuddy.Core.Tests.Support;

/// <summary>
/// Writes sample image files like a phone or scanner does: filled with one colour, with a black bar
/// along the top edge of the stored pixels, so tests can tell images apart and see which way they
/// were turned. Optionally a magenta bar along the left edge as well, to see whether they were mirrored.
/// </summary>
public static class SampleImages
{
    public static readonly (int R, int G, int B) Red = (220, 30, 30);
    public static readonly (int R, int G, int B) Green = (30, 170, 60);
    public static readonly (int R, int G, int B) Blue = (30, 60, 200);
    public static readonly (int R, int G, int B) Magenta = (200, 0, 200);

    /// <param name="dpi">The resolution the file states, as a scanner writes it.</param>
    public static Task<string> CreateJpegAsync(
        string folder, string fileName, int width, int height, (int R, int G, int B) colour, ushort exifOrientation = 1,
        bool markLeft = false, double dpi = 96) =>
        CreateAsync(folder, fileName, BitmapEncoder.JpegEncoderId, width, height, colour, exifOrientation, markLeft, dpi);

    public static Task<string> CreatePngAsync(string folder, string fileName, int width, int height, (int R, int G, int B) colour) =>
        CreateAsync(folder, fileName, BitmapEncoder.PngEncoderId, width, height, colour, exifOrientation: 1, markLeft: false, dpi: 96);

    /// <summary>
    /// A scanned signature: a black stroke across the middle third of its height, on transparency as
    /// a PNG, or <paramref name="onWhitePaper"/> as a JPG like a phone photo of it.
    /// </summary>
    public static async Task<string> CreateSignatureAsync(string folder, string fileName, int width, int height, bool onWhitePaper = false)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var isStroke = y >= height / 3 && y < 2 * height / 3;
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                if (isStroke)
                {
                    pixels[i + 3] = 255;
                }
                else if (onWhitePaper)
                {
                    (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (255, 255, 255, 255);
                }
            }
        }

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(onWhitePaper ? BitmapEncoder.JpegEncoderId : BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)width, (uint)height, 96, 96, pixels);
        await encoder.FlushAsync();
        return await WriteAsync(folder, fileName, stream);
    }

    /// <summary>The start of a HEIC photo, as iPhones save them.</summary>
    public static string CreateHeic(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        File.WriteAllBytes(path, [0, 0, 0, 0x18, .."ftypheic"u8, 0, 0, 0, 0, .."mif1heic"u8, .. new byte[256]]);
        return path;
    }

    private static async Task<string> CreateAsync(
        string folder, string fileName, Guid encoderId, int width, int height, (int R, int G, int B) colour, ushort exifOrientation,
        bool markLeft, double dpi)
    {
        var (bar, leftBar) = (height / 6, markLeft ? width / 6 : 0);
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = y < bar ? (0, 0, 0) : x < leftBar ? Magenta : colour;
                var i = (y * width + x) * 4;
                (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = ((byte)b, (byte)g, (byte)r, 255);
            }
        }

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)width, (uint)height, dpi, dpi, pixels);
        if (exifOrientation != 1)
        {
            await encoder.BitmapProperties.SetPropertiesAsync(
                [new("System.Photo.Orientation", new BitmapTypedValue(exifOrientation, PropertyType.UInt16))]);
        }
        await encoder.FlushAsync();
        return await WriteAsync(folder, fileName, stream);
    }

    private static async Task<string> WriteAsync(string folder, string fileName, InMemoryRandomAccessStream stream)
    {
        var content = new byte[stream.Size];
        stream.Seek(0);
        await stream.ReadAsync(content.AsBuffer(), (uint)content.Length, InputStreamOptions.None);
        var path = Path.Combine(folder, fileName);
        await File.WriteAllBytesAsync(path, content);
        return path;
    }
}
