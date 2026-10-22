using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Images;
using Windows.Graphics.Imaging;

namespace OfficeBuddy.Core.Tools;

/// <summary>
/// The part of an image that is kept: its top left corner, width and height, each as a share of
/// the turned image's width or height, from 0 to 1.
/// </summary>
public sealed record CropArea(double Left, double Top, double Width, double Height)
{
    public static CropArea Whole { get; } = new(0, 0, 1, 1);

    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

/// <summary>How far the image is turned clockwise beyond the right way up (0, 90, 180 or 270 degrees), and what is kept of it.</summary>
public sealed record Crop(int Rotation, CropArea Area);

/// <summary>
/// The state of the Crop image Tool: one JPG or PNG image, turned in 90° steps, and the part of it
/// that is kept. Every change can be undone.
/// </summary>
public sealed class CropImageSession() : ToolSession<Crop?>(null)
{
    // Never smaller than this, so a crop can't end up as a dot.
    private const int MinPixels = 8;

    private static readonly Crop Uncut = new(0, CropArea.Whole);

    public ImageFile? Image { get; private set; }

    public int Rotation => State?.Rotation ?? 0;

    public CropArea Area => State?.Area ?? CropArea.Whole;

    /// <summary>The size of the whole image as it is turned now.</summary>
    public ImageSize? TurnedSize => Image is null ? null : Turned(Image, Rotation);

    /// <summary>The size of the image that gets saved.</summary>
    public ImageSize? ResultSize
    {
        get
        {
            if (State is null)
            {
                return null;
            }

            var bounds = ToPixels(Image!, State);
            return new ImageSize((int)bounds.Width, (int)bounds.Height);
        }
    }

    /// <summary>Saving is worth it once the image is cut or turned.</summary>
    public bool CanSave => State is not null && State != Uncut;

    /// <summary>E.g. <c>IMG_0815 (zugeschnitten).jpg</c>, a PNG staying a PNG.</summary>
    public string? SuggestedFileName => Image is null
        ? null
        : $"{Image.NameWithoutExtension} (zugeschnitten).{(Image.IsPng ? "png" : "jpg")}";

    public string? SuggestedFolder => Image?.Folder;

    /// <summary>
    /// Opens the image, replacing the current one, whole and the right way up: nothing to undo or
    /// save. Nothing is changed if the file cannot be opened.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public async Task<LoadFailure?> OpenAsync(string filePath)
    {
        var result = await ImageFile.LoadAllAsync([filePath]);
        if (result.Failures.Count > 0)
        {
            return result.Failures[0];
        }

        Image = result.Images[0];
        Reset(Uncut);
        return null;
    }

    /// <summary>
    /// Keeps <paramref name="area"/>. It stays on the image and at least a few pixels big: a side
    /// too long is shortened, an area partly outside is moved back in. No change if it is the same.
    /// </summary>
    public void SetArea(CropArea area)
    {
        var crop = State!;
        var size = Turned(Image!, crop.Rotation);
        var width = Math.Clamp(area.Width, Math.Min(1, (double)MinPixels / size.Width), 1);
        var height = Math.Clamp(area.Height, Math.Min(1, (double)MinPixels / size.Height), 1);
        var fitted = new CropArea(Math.Clamp(area.Left, 0, 1 - width), Math.Clamp(area.Top, 0, 1 - height), width, height);
        if (fitted != crop.Area)
        {
            Apply(crop with { Area = fitted });
        }
    }

    /// <summary>Keeps the whole image again, as it is turned now.</summary>
    public void KeepWhole() => SetArea(CropArea.Whole);

    /// <summary>Turns the image a quarter clockwise; the kept part turns with it.</summary>
    public void RotateRight()
    {
        var (rotation, area) = State!;
        Apply(new Crop((rotation + 90) % 360, new CropArea(1 - area.Bottom, area.Left, area.Height, area.Width)));
    }

    /// <summary>Turns the image a quarter anticlockwise; the kept part turns with it.</summary>
    public void RotateLeft()
    {
        var (rotation, area) = State!;
        Apply(new Crop((rotation + 270) % 360, new CropArea(area.Top, 1 - area.Right, area.Height, area.Width)));
    }

    public async Task SaveAsync(string outputPath)
    {
        var (image, crop) = (Image!, State!);
        await File.WriteAllBytesAsync(outputPath, await image.CropAsync(crop.Rotation, ToPixels(image, crop)));
        MarkSaved(crop);
    }

    private static ImageSize Turned(ImageFile image, int rotation) =>
        rotation is 90 or 270 ? new ImageSize(image.Height, image.Width) : new ImageSize(image.Width, image.Height);

    /// <summary>The kept part in pixels of the turned image, at least one pixel big.</summary>
    private static BitmapBounds ToPixels(ImageFile image, Crop crop)
    {
        var (size, area) = (Turned(image, crop.Rotation), crop.Area);
        var left = Math.Min(size.Width - 1, (int)Math.Round(area.Left * size.Width));
        var top = Math.Min(size.Height - 1, (int)Math.Round(area.Top * size.Height));
        var right = Math.Clamp((int)Math.Round(area.Right * size.Width), left + 1, size.Width);
        var bottom = Math.Clamp((int)Math.Round(area.Bottom * size.Height), top + 1, size.Height);
        return new BitmapBounds { X = (uint)left, Y = (uint)top, Width = (uint)(right - left), Height = (uint)(bottom - top) };
    }
}
