namespace PdfManager.Core;

/// <summary>An image's size in pixels, the right way up.</summary>
public sealed record ImageSize(int Width, int Height)
{
    public long Pixels => (long)Width * Height;
}

/// <summary>
/// The state of the Resize image Tool: one JPG or PNG image and the size it will be saved at, set
/// either as a percentage or as a width and height in pixels. Every new size can be undone.
/// </summary>
public sealed class ResizeImageSession() : ToolSession<ImageSize?>(null)
{
    // A bigger image would hardly fit in memory; larger sizes are scaled down to this, as for Page to image.
    private const long MaxPixels = 64_000_000;

    public ImageFile? Image { get; private set; }

    /// <summary>The size of the opened image, the right way up.</summary>
    public ImageSize? OriginalSize { get; private set; }

    /// <summary>The size the image will be saved at.</summary>
    public ImageSize? Size => State;

    /// <summary>
    /// The new size as a percentage of the original, or null while width and height are not changed
    /// in proportion.
    /// </summary>
    public double? Percentage =>
        State is null || OriginalSize is null || !IsInProportion(State) ? null : 100.0 * State.Width / OriginalSize.Width;

    /// <summary>Saving is worth it once the size differs from the original.</summary>
    public bool CanSave => State is not null && State != OriginalSize;

    /// <summary>E.g. <c>IMG_0815 (800x600).jpg</c>, a PNG staying a PNG.</summary>
    public string? SuggestedFileName => Image is null || State is null
        ? null
        : $"{Image.NameWithoutExtension} ({State.Width}x{State.Height}).{(Image.IsPng ? "png" : "jpg")}";

    public string? SuggestedFolder => Image?.Folder;

    /// <summary>
    /// Opens the image, replacing the current one, at its original size: nothing to undo or save.
    /// Nothing is changed if the file cannot be opened.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public async Task<LoadFailure?> OpenAsync(string filePath)
    {
        var result = await ImageFile.LoadAllAsync([filePath]);
        if (result.Failures.Count > 0)
        {
            return result.Failures[0];
        }

        var image = result.Images[0];
        Image = image;
        OriginalSize = new ImageSize(image.Width, image.Height);
        Reset(OriginalSize);
        return null;
    }

    /// <summary>Scales width and height alike, e.g. 50 for half the size in each direction.</summary>
    public void SetPercentage(double percentage)
    {
        var original = OriginalSize!;
        SetSize(new ImageSize(Scale(original.Width, percentage / 100), Scale(original.Height, percentage / 100)));
    }

    /// <summary>Sets the width in pixels; the height follows in proportion if <paramref name="keepProportions"/>.</summary>
    public void SetWidth(int width, bool keepProportions)
    {
        var original = OriginalSize!;
        width = Math.Max(1, width);
        SetSize(new ImageSize(width, keepProportions ? Scale(original.Height, (double)width / original.Width) : State!.Height));
    }

    /// <summary>Sets the height in pixels; the width follows in proportion if <paramref name="keepProportions"/>.</summary>
    public void SetHeight(int height, bool keepProportions)
    {
        var original = OriginalSize!;
        height = Math.Max(1, height);
        SetSize(new ImageSize(keepProportions ? Scale(original.Width, (double)height / original.Height) : State!.Width, height));
    }

    public async Task SaveAsync(string outputPath)
    {
        var size = State!;
        await File.WriteAllBytesAsync(outputPath, await Image!.ResizeAsync(size.Width, size.Height));
        MarkSaved(size);
    }

    /// <summary>Makes <paramref name="size"/> the new size, scaled down if it would be too big; no change if it is the same.</summary>
    private void SetSize(ImageSize size)
    {
        if (size.Pixels > MaxPixels)
        {
            // Rounded down, so the scaled image stays under the limit.
            var scale = Math.Sqrt((double)MaxPixels / size.Pixels);
            size = new ImageSize(Math.Max(1, (int)(size.Width * scale)), Math.Max(1, (int)(size.Height * scale)));
        }

        if (size != State)
        {
            Apply(size);
        }
    }

    /// <summary>Whether one side is the other scaled like the original, within rounding.</summary>
    private bool IsInProportion(ImageSize size)
    {
        var original = OriginalSize!;
        return size.Height == Scale(original.Height, (double)size.Width / original.Width)
            || size.Width == Scale(original.Width, (double)size.Height / original.Height);
    }

    private static int Scale(int pixels, double factor) => Math.Max(1, (int)Math.Round(pixels * factor));
}
