using System.Collections.Immutable;

namespace OfficeBuddy.Core;

/// <summary>How Images side by side lines the images up.</summary>
public enum ImageArrangement
{
    /// <summary>In a row from left to right, all the same height.</summary>
    SideBySide,

    /// <summary>In a column from top to bottom, all the same width.</summary>
    OneBelowTheOther,
}

/// <summary>The images in the order they are lined up, and how.</summary>
public sealed record ImagesSideBySideState(ImmutableList<ImageFile> Images, ImageArrangement Arrangement);

/// <summary>
/// The state of the Images side by side Tool: two or more JPG and PNG images that are saved as one
/// image, lined up in a row or a column with a thin white gap between them. Every change can be undone.
/// </summary>
public sealed class ImagesSideBySideSession() : ToolSession<ImagesSideBySideState>(new([], ImageArrangement.SideBySide))
{
    // The white gap between two images, as a share of the height they share in a row (the width in a column).
    private const double GapShare = 0.02;

    public IReadOnlyList<ImageFile> Images => State.Images;

    public ImageArrangement Arrangement => State.Arrangement;

    public bool CanSave => State.Images.Count >= 2;

    /// <summary>A PNG when every image is one, so drawings stay sharp; otherwise a JPG, as photos are.</summary>
    public bool SavesAsPng => State.Images.Count > 0 && State.Images.All(image => image.IsPng);

    /// <summary>The size of the image that gets saved, or null while there is nothing to line up.</summary>
    public ImageSize? ResultSize => State.Images.Count == 0 ? null : Lay(State).Size;

    /// <summary>E.g. <c>Ausweis vorne (nebeneinander).jpg</c>, after the first image.</summary>
    public string? SuggestedFileName => State.Images.Count == 0
        ? null
        : $"{State.Images[0].NameWithoutExtension} ({(State.Arrangement == ImageArrangement.SideBySide ? "nebeneinander" : "untereinander")}).{(SavesAsPng ? "png" : "jpg")}";

    public string? SuggestedFolder => State.Images.Count == 0 ? null : State.Images[0].Folder;

    /// <summary>
    /// Adds the images that can be opened, in the given order, at the end. A file that cannot be
    /// opened does not stop the others.
    /// </summary>
    /// <returns>The files that could not be opened, and why.</returns>
    public async Task<IReadOnlyList<LoadFailure>> AddAsync(IEnumerable<string> filePaths)
    {
        var result = await ImageFile.LoadAllAsync(filePaths);
        if (result.Images.Count > 0)
        {
            Apply(State with { Images = State.Images.AddRange(result.Images) });
        }
        return result.Failures;
    }

    public void Move(int from, int to) =>
        Apply(State with { Images = State.Images.RemoveAt(from).Insert(to, State.Images[from]) });

    public void Remove(int index) => Apply(State with { Images = State.Images.RemoveAt(index) });

    public void Arrange(ImageArrangement arrangement)
    {
        if (arrangement != State.Arrangement)
        {
            Apply(State with { Arrangement = arrangement });
        }
    }

    public async Task SaveAsync(string outputPath)
    {
        // Saved in the background as it is now; later changes don't affect it.
        var state = State;
        var asPng = SavesAsPng;
        var content = await Task.Run(async () => await (await ComposeAsync(state, Lay(state))).EncodeAsync(asPng));
        await File.WriteAllBytesAsync(outputPath, content);
        MarkSaved(state);
    }

    /// <summary>
    /// Renders the image that would be saved now as a PNG that fits inside the given pixel size,
    /// e.g. for a preview. Needs at least one image.
    /// </summary>
    public async Task<byte[]> RenderPreviewPngAsync(int maxWidth, int maxHeight)
    {
        var state = State;
        return await Task.Run(async () =>
        {
            var full = Lay(state);
            var scale = Math.Min(1, Math.Min((double)maxWidth / full.Size.Width, (double)maxHeight / full.Size.Height));
            var preview = Lay(state, Math.Max(1, (int)(full.Shared * scale)));
            return await (await ComposeAsync(state, preview)).EncodeAsync(asPng: true);
        });
    }

    private static async Task<ImageCanvas> ComposeAsync(ImagesSideBySideState state, Layout layout)
    {
        var canvas = ImageCanvas.White(layout.Size.Width, layout.Size.Height);
        for (var i = 0; i < state.Images.Count; i++)
        {
            var (left, top, width, height) = layout.Places[i];
            canvas.Draw(await state.Images[i].ReadPixelsAsync(width, height), left, top);
        }
        return canvas;
    }

    /// <summary>
    /// Where each image goes. All share the height of the lowest one in a row (the width of the
    /// narrowest in a column), so none gets blurred by being made bigger; the whole is scaled down
    /// only if it would be too big.
    /// </summary>
    private static Layout Lay(ImagesSideBySideState state)
    {
        var sideBySide = state.Arrangement == ImageArrangement.SideBySide;
        var shared = state.Images.Min(image => sideBySide ? image.Height : image.Width);
        var layout = Lay(state, shared);
        if (layout.Size.Pixels <= ImageCanvas.MaxPixels)
        {
            return layout;
        }

        // Rounded down, so the scaled result stays under the limit.
        return Lay(state, Math.Max(1, (int)(shared * Math.Sqrt((double)ImageCanvas.MaxPixels / layout.Size.Pixels))));
    }

    /// <param name="shared">The height every image gets in a row, or the width in a column.</param>
    private static Layout Lay(ImagesSideBySideState state, int shared)
    {
        var sideBySide = state.Arrangement == ImageArrangement.SideBySide;
        var gap = (int)Math.Round(shared * GapShare);
        var places = new List<(int Left, int Top, int Width, int Height)>();
        var along = 0;
        foreach (var image in state.Images)
        {
            if (places.Count > 0)
            {
                along += gap;
            }

            if (sideBySide)
            {
                var width = Math.Max(1, (int)Math.Round((double)image.Width * shared / image.Height));
                places.Add((along, 0, width, shared));
                along += width;
            }
            else
            {
                var height = Math.Max(1, (int)Math.Round((double)image.Height * shared / image.Width));
                places.Add((0, along, shared, height));
                along += height;
            }
        }
        return new Layout(places, sideBySide ? new ImageSize(along, shared) : new ImageSize(shared, along), shared);
    }

    private sealed record Layout(IReadOnlyList<(int Left, int Top, int Width, int Height)> Places, ImageSize Size, int Shared);
}
