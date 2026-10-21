using System.Collections.Immutable;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PdfManager.Core;

/// <summary>
/// The state of the Images to PDF Tool: JPG and PNG images in the order they will become Pages, one
/// A4 Page each.
/// </summary>
public sealed class ImagesToPdfSession() : ToolSession<ImmutableList<ImageFile>>([])
{
    // A4, upright, in millimetres, and the white margin around every image.
    private const double A4Width = 210;
    private const double A4Height = 297;
    private const double Margin = 10;

    public IReadOnlyList<ImageFile> Images => State;

    public bool CanSave => State.Count >= 1;

    /// <summary>E.g. <c>IMG_0815.pdf</c>, after the first image.</summary>
    public string? SuggestedFileName => State.Count == 0 ? null : $"{State[0].NameWithoutExtension}.pdf";

    public string? SuggestedFolder => State.Count == 0 ? null : State[0].Folder;

    /// <summary>
    /// Adds the images that can be opened, in the given order. A file that cannot be opened does
    /// not stop the others.
    /// </summary>
    /// <returns>The files that could not be opened, and why.</returns>
    public async Task<IReadOnlyList<LoadFailure>> AddAsync(IEnumerable<string> filePaths)
    {
        var result = await ImageFile.LoadAllAsync(filePaths);
        if (result.Images.Count > 0)
        {
            Apply(State.AddRange(result.Images));
        }
        return result.Failures;
    }

    public void Move(int from, int to) => Apply(State.RemoveAt(from).Insert(to, State[from]));

    public void Remove(int index) => Apply(State.RemoveAt(index));

    public async Task SaveAsync(string outputPath)
    {
        // Saved in the background from the list as it is now; later changes don't affect it.
        var images = State;
        var content = await Task.Run(() => Build(images));
        await File.WriteAllBytesAsync(outputPath, content);
        MarkSaved(images);
    }

    private static byte[] Build(IEnumerable<ImageFile> images)
    {
        using var document = new PdfDocument();
        foreach (var image in images)
        {
            AddPage(document, image);
        }

        using var buffer = new MemoryStream();
        document.Save(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    /// One A4 Page, landscape for a landscape image, with the image the right way up, as large as
    /// fits inside the margin, centred and in its own proportions.
    /// </summary>
    private static void AddPage(PdfDocument document, ImageFile image)
    {
        var page = document.AddPage();
        page.Width = XUnit.FromMillimeter(image.IsLandscape ? A4Height : A4Width);
        page.Height = XUnit.FromMillimeter(image.IsLandscape ? A4Width : A4Height);

        var margin = XUnit.FromMillimeter(Margin).Point;
        var (pageWidth, pageHeight) = (page.Width.Point, page.Height.Point);
        var scale = Math.Min((pageWidth - 2 * margin) / image.Width, (pageHeight - 2 * margin) / image.Height);
        var (width, height) = (image.Width * scale, image.Height * scale);

        using var graphics = XGraphics.FromPdfPage(page);
        image.Draw(graphics, new XPoint(pageWidth / 2, pageHeight / 2), width, height);
    }
}
