using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Images;

namespace OfficeBuddy.Core.Tools;

/// <summary>An image placed on top of the Main image, and where.</summary>
public sealed record InsertedImage(ImageFile Image, Placement Placement);

/// <summary>
/// The state of the Picture in picture Tool: one JPG or PNG image, the Main image, and at most one
/// Inserted image placed on top of it. Inserting, moving, sizing and removing can all be undone.
/// </summary>
public sealed class PictureInPictureSession() : ToolSession<InsertedImage?>(null)
{
    // A newly inserted image takes up at most a third of the Main image's width and height.
    private const double StartingShare = 1.0 / 3;

    // Never narrower than this share of the Main image, so it stays visible and can be grabbed.
    private const double MinWidth = 0.02;

    public ImageFile? MainImage { get; private set; }

    public InsertedImage? Inserted => State;

    public bool CanSave => State is not null;

    /// <summary>E.g. <c>IMG_0815 (Bild in Bild).jpg</c>, after the Main image, a PNG staying a PNG.</summary>
    public string? SuggestedFileName => MainImage is null
        ? null
        : $"{MainImage.NameWithoutExtension} (Bild in Bild).{(MainImage.IsPng ? "png" : "jpg")}";

    public string? SuggestedFolder => MainImage?.Folder;

    /// <summary>
    /// Opens the Main image, replacing the current one, without an Inserted image: nothing to undo
    /// or save. Nothing is changed if the file cannot be opened.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public async Task<LoadFailure?> OpenAsync(string filePath)
    {
        var result = await ImageFile.LoadAllAsync([filePath]);
        if (result.Failures.Count > 0)
        {
            return result.Failures[0];
        }

        MainImage = result.Images[0];
        Reset(null);
        return null;
    }

    /// <summary>
    /// Places the image on the Main image, in the middle and at most a third as wide and high,
    /// replacing an image inserted before. Nothing is changed if the file cannot be opened.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public async Task<LoadFailure?> InsertAsync(string filePath)
    {
        var result = await ImageFile.LoadAllAsync([filePath]);
        if (result.Failures.Count > 0)
        {
            return result.Failures[0];
        }

        var image = result.Images[0];
        var width = Math.Min(StartingShare, StartingShare / HeightPerWidth(image));
        var height = width * HeightPerWidth(image);
        Apply(new InsertedImage(image, Fit(image, new Placement((1 - width) / 2, (1 - height) / 2, width))));
        return null;
    }

    /// <summary>Takes the Inserted image off the Main image.</summary>
    public void Remove()
    {
        if (State is not null)
        {
            Apply(null);
        }
    }

    /// <summary>
    /// Moves and sizes the Inserted image. It stays whole on the Main image: a place partly outside
    /// is moved back in, and a width too big to fit is made smaller. No change if it is already there.
    /// </summary>
    public void Place(Placement placement)
    {
        var inserted = State!;
        var fitted = Fit(inserted.Image, placement);
        if (fitted != inserted.Placement)
        {
            Apply(inserted with { Placement = fitted });
        }
    }

    /// <summary>The Inserted image's height as a share of the Main image's height, at the given width share.</summary>
    public double HeightFor(double width) => width * HeightPerWidth(State!.Image);

    public async Task SaveAsync(string outputPath)
    {
        // Saved in the background as it is now; later changes don't affect it.
        var (main, inserted) = (MainImage!, State!);
        var content = await Task.Run(() => ComposeAsync(main, inserted));
        await File.WriteAllBytesAsync(outputPath, content);
        MarkSaved(inserted);
    }

    /// <summary>The Main image at its own size, scaled down only if too big, with the Inserted image drawn on top.</summary>
    private static async Task<byte[]> ComposeAsync(ImageFile main, InsertedImage inserted)
    {
        var scale = Math.Min(1, Math.Sqrt((double)ImageCanvas.MaxPixels / ((long)main.Width * main.Height)));
        var (width, height) = (Math.Max(1, (int)(main.Width * scale)), Math.Max(1, (int)(main.Height * scale)));
        var canvas = await main.ReadPixelsAsync(width, height);

        var (image, placement) = (inserted.Image, inserted.Placement);
        var insertedWidth = Math.Max(1, (int)Math.Round(placement.Width * width));
        var insertedHeight = Math.Max(1, (int)Math.Round(insertedWidth * (double)image.Height / image.Width));
        var picture = await image.ReadPixelsAsync(insertedWidth, insertedHeight);
        canvas.Draw(picture, (int)Math.Round(placement.Left * width), (int)Math.Round(placement.Top * height));
        return await canvas.EncodeAsync(main.IsPng);
    }

    /// <summary>The place nearest to <paramref name="placement"/> where <paramref name="image"/> lies whole on the Main image.</summary>
    private Placement Fit(ImageFile image, Placement placement) => placement.Fit(HeightPerWidth(image), MinWidth);

    /// <summary>How many shares of the Main image's height <paramref name="image"/> takes per share of its width.</summary>
    private double HeightPerWidth(ImageFile image) =>
        (double)image.Height / image.Width * MainImage!.Width / MainImage.Height;
}
