using System.Runtime.InteropServices.WindowsRuntime;
using PdfSharp.Drawing;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PdfManager.Core;

/// <summary>
/// How an image's stored pixels must be turned to show it the right way up, from the orientation a
/// phone notes in the photo's EXIF data: first mirrored left to right if <paramref name="Mirrored"/>,
/// then rotated clockwise by <paramref name="Rotation"/> degrees.
/// </summary>
internal readonly record struct ImageTurn(bool Mirrored, int Rotation)
{
    public bool SwapsSides => Rotation is 90 or 270;

    public static ImageTurn FromExifOrientation(ushort orientation) => orientation switch
    {
        2 => new(true, 0),
        3 => new(false, 180),
        4 => new(true, 180),
        5 => new(true, 270),
        6 => new(false, 90),
        7 => new(true, 90),
        8 => new(false, 270),
        _ => new(false, 0),
    };
}

/// <summary>
/// A JPG or PNG image added to Images to PDF, such as a phone photo of a letter. Its content is read
/// fully into memory, like a Document's.
/// </summary>
public sealed class ImageFile
{
    private static readonly byte[] JpegStart = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngStart = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly int _storedWidth;
    private readonly int _storedHeight;

    private ImageFile(string filePath, byte[] content, int storedWidth, int storedHeight, ImageTurn turn)
    {
        FilePath = filePath;
        Content = content;
        (_storedWidth, _storedHeight) = (storedWidth, storedHeight);
        Turn = turn;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public string NameWithoutExtension => Path.GetFileNameWithoutExtension(FilePath);

    public string? Folder => Path.GetDirectoryName(FilePath);

    /// <summary>The width in pixels, the right way up.</summary>
    public int Width => Turn.SwapsSides ? _storedHeight : _storedWidth;

    /// <summary>The height in pixels, the right way up.</summary>
    public int Height => Turn.SwapsSides ? _storedWidth : _storedHeight;

    /// <summary>Wider than high, the right way up; such an image gets a landscape Page.</summary>
    public bool IsLandscape => Width > Height;

    /// <summary>The image as PDFsharp can embed it: usually the file itself, pixels as stored.</summary>
    private byte[] Content { get; }

    private ImageTurn Turn { get; }

    /// <summary>
    /// Opens the files as images, in the given order. A file that cannot be opened is reported with
    /// the reason (anything but JPG and PNG is unsupported) and does not stop the others.
    /// </summary>
    public static async Task<ImageLoadResult> LoadAllAsync(IEnumerable<string> filePaths)
    {
        var images = new List<ImageFile>();
        var failures = new List<LoadFailure>();
        foreach (var filePath in filePaths)
        {
            try
            {
                images.Add(await LoadAsync(filePath));
            }
            catch (LoadFailedException ex)
            {
                failures.Add(new LoadFailure(filePath, ex.Reason));
            }
        }
        return new ImageLoadResult(images, failures);
    }

    private static async Task<ImageFile> LoadAsync(string filePath)
    {
        byte[] content;
        try
        {
            content = await File.ReadAllBytesAsync(filePath);
        }
        catch (Exception)
        {
            // Gone, in use by another program, no permission to read it, or an unusable path.
            throw new LoadFailedException(LoadFailureReason.Damaged);
        }

        // Recognised by content, not by name: a HEIC photo renamed to .jpg is still not a JPG.
        if (!content.AsSpan().StartsWith(JpegStart) && !content.AsSpan().StartsWith(PngStart))
        {
            throw new LoadFailedException(LoadFailureReason.Unsupported);
        }

        try
        {
            using var stream = await ToStreamAsync(content);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var turn = ImageTurn.FromExifOrientation(await ReadExifOrientationAsync(decoder));
            var readable = await Task.Run(() => CanEmbed(content)) ? content : await ToPngAsync(decoder);
            return new ImageFile(filePath, readable, (int)decoder.PixelWidth, (int)decoder.PixelHeight, turn);
        }
        catch (Exception)
        {
            // Cut off or otherwise broken: Windows can't decode it.
            throw new LoadFailedException(LoadFailureReason.Damaged);
        }
    }

    /// <summary>
    /// Draws the image the right way up, <paramref name="width"/> by <paramref name="height"/> points,
    /// centred on <paramref name="centre"/>. The file's own pixels are embedded as they are and
    /// turned as the photo's EXIF data says.
    /// </summary>
    internal void Draw(XGraphics graphics, XPoint centre, double width, double height)
    {
        using var picture = XImage.FromStream(new MemoryStream(Content, writable: false));
        var state = graphics.Save();
        graphics.TranslateTransform(centre.X, centre.Y);
        graphics.RotateTransform(Turn.Rotation);
        if (Turn.Mirrored)
        {
            graphics.ScaleTransform(-1, 1);
        }
        var (storedWidth, storedHeight) = Turn.SwapsSides ? (height, width) : (width, height);
        graphics.DrawImage(picture, -storedWidth / 2, -storedHeight / 2, storedWidth, storedHeight);
        graphics.Restore(state);
    }

    /// <summary>
    /// Renders the image the right way up as a PNG that fits inside the given pixel size, keeping its
    /// proportions, e.g. for a thumbnail.
    /// </summary>
    public async Task<byte[]> RenderPngAsync(int maxWidth, int maxHeight)
    {
        using var source = await ToStreamAsync(Content);
        var decoder = await BitmapDecoder.CreateAsync(source);
        var scale = Math.Min(1, Math.Min((double)maxWidth / Width, (double)maxHeight / Height));
        var transform = new BitmapTransform
        {
            // In stored pixels, before the turn is applied.
            ScaledWidth = (uint)Math.Max(1, Math.Round(_storedWidth * scale)),
            ScaledHeight = (uint)Math.Max(1, Math.Round(_storedHeight * scale)),
            InterpolationMode = BitmapInterpolationMode.Fant,
        };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
        return await EncodePngAsync(bitmap);
    }

    private static async Task<ushort> ReadExifOrientationAsync(BitmapDecoder decoder)
    {
        try
        {
            var properties = await decoder.BitmapProperties.GetPropertiesAsync(["System.Photo.Orientation"]);
            return properties.TryGetValue("System.Photo.Orientation", out var orientation) ? (ushort)orientation.Value : (ushort)1;
        }
        catch (Exception)
        {
            // PNGs and photos without EXIF data are stored upright.
            return 1;
        }
    }

    private static bool CanEmbed(byte[] content)
    {
        try
        {
            using var image = XImage.FromStream(new MemoryStream(content, writable: false));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The stored pixels as a PNG, for the rare JPG or PNG variant PDFsharp can't embed as it is.</summary>
    private static async Task<byte[]> ToPngAsync(BitmapDecoder decoder)
    {
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        return await EncodePngAsync(bitmap);
    }

    private static async Task<byte[]> EncodePngAsync(SoftwareBitmap bitmap)
    {
        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();

        var png = new byte[output.Size];
        output.Seek(0);
        await output.ReadAsync(png.AsBuffer(), (uint)png.Length, InputStreamOptions.None);
        return png;
    }

    private static async Task<InMemoryRandomAccessStream> ToStreamAsync(byte[] content)
    {
        var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(content.AsBuffer());
        stream.Seek(0);
        return stream;
    }
}

/// <summary>The images that were opened and the files that could not be opened.</summary>
public sealed record ImageLoadResult(IReadOnlyList<ImageFile> Images, IReadOnlyList<LoadFailure> Failures);
