using System.Collections.Immutable;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OfficeBuddy.Core;

/// <summary>A scanned Page: the scanner's picture, turned a further <paramref name="Rotation"/> degrees clockwise.</summary>
public sealed record ScannedPage(ImageFile Image, int Rotation = 0)
{
    /// <summary>Wider than high as it will be saved.</summary>
    public bool IsLandscape => Rotation is 90 or 270 ? Image.Height > Image.Width : Image.IsLandscape;
}

/// <summary>
/// The state of the Scan Tool: the Pages scanned so far, in the order they will be saved as one
/// Document, each at the size of the paper that was scanned. Every change can be undone.
/// </summary>
public sealed class ScanSession() : ToolSession<ImmutableList<ScannedPage>>([])
{
    // The long side of A4 in points: the Page size for a picture that doesn't state a believable resolution.
    private const double A4LongSide = 841.89;

    private DateTime? _firstScanAt;

    public IReadOnlyList<ScannedPage> Pages => State;

    public bool CanSave => State.Count > 0;

    /// <summary>E.g. <c>Scan 2026-10-22 14.05.pdf</c>, from when the first Page was scanned.</summary>
    public string SuggestedFileName => $"Scan {_firstScanAt ?? DateTime.Now:yyyy-MM-dd HH.mm}.pdf";

    /// <summary>The user's Documents folder: scans come from no file of their own.</summary>
    public string SuggestedFolder => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    /// <summary>
    /// Adds scanned pictures (or other JPG and PNG images) as Pages at the end, in the given order.
    /// A file that cannot be opened does not stop the others. The files can be deleted afterwards.
    /// </summary>
    /// <returns>The files that could not be opened, and why.</returns>
    public async Task<IReadOnlyList<LoadFailure>> AddAsync(IEnumerable<string> filePaths)
    {
        var result = await ImageFile.LoadAllAsync(filePaths);
        if (result.Images.Count > 0)
        {
            _firstScanAt ??= DateTime.Now;
            Apply(State.AddRange(result.Images.Select(image => new ScannedPage(image))));
        }
        return result.Failures;
    }

    public void Move(int from, int to) => Apply(State.RemoveAt(from).Insert(to, State[from]));

    public void Remove(int index) => Apply(State.RemoveAt(index));

    /// <summary>Turns the Page a quarter clockwise, e.g. one that went through the scanner sideways.</summary>
    public void RotateRight(int index) => Apply(State.SetItem(index, State[index] with { Rotation = (State[index].Rotation + 90) % 360 }));

    /// <summary>Turns the Page a quarter anticlockwise.</summary>
    public void RotateLeft(int index) => Apply(State.SetItem(index, State[index] with { Rotation = (State[index].Rotation + 270) % 360 }));

    public async Task SaveAsync(string outputPath)
    {
        // Saved in the background from the Pages as they are now; later changes don't affect it.
        var pages = State;
        var content = await Task.Run(() =>
        {
            using var document = new PdfDocument();
            foreach (var scanned in pages)
            {
                AddPage(document, scanned);
            }
            return Document.Save(document);
        });
        await File.WriteAllBytesAsync(outputPath, content);
        MarkSaved(pages);
    }

    /// <summary>The size in points of the picture the right way up, before any further turn.</summary>
    internal static (double Width, double Height) PaperSize(ImageFile image)
    {
        // A scanner states the real resolution; anything outside what scanners use is only a default.
        var pointsPerPixel = image.Dpi is >= 100 and <= 1200
            ? 72 / image.Dpi
            : A4LongSide / Math.Max(image.Width, image.Height);
        return (image.Width * pointsPerPixel, image.Height * pointsPerPixel);
    }

    /// <summary>One Page exactly the size of the scanned paper, filled by its picture, turned as the user turned it.</summary>
    private static void AddPage(PdfDocument document, ScannedPage scanned)
    {
        var (width, height) = PaperSize(scanned.Image);
        var turned = scanned.Rotation is 90 or 270;
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(turned ? height : width);
        page.Height = XUnit.FromPoint(turned ? width : height);

        using var graphics = XGraphics.FromPdfPage(page);
        var centre = new XPoint(page.Width.Point / 2, page.Height.Point / 2);
        graphics.RotateAtTransform(scanned.Rotation, centre);
        scanned.Image.Draw(graphics, centre, width, height);
    }
}
