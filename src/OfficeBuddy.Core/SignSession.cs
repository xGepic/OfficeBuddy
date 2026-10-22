using System.Collections.Immutable;
using PdfSharp.Drawing;

namespace OfficeBuddy.Core;

/// <summary>
/// The signature image and where it goes: on which Pages (by index), and where on each, as a share
/// of the Page as shown (see <see cref="Placement"/>).
/// </summary>
public sealed record Signing(ImageFile? Signature, ImmutableDictionary<int, Placement> Placements);

/// <summary>
/// The state of the Sign Tool: one Document, a signature image (a scan or photo of the user's
/// signature, its paper made transparent), and the Pages it is put on, at most once each. The
/// signature stays chosen when another Document is opened. Every change can be undone.
/// </summary>
public sealed class SignSession() : DocumentSession<Signing>(new(null, []))
{
    // A newly placed signature is this share of the Page's width, unless that makes it too high.
    private const double StartingWidth = 0.3;
    private const double StartingMaxHeight = 0.12;

    // Never narrower than this share of the Page, so it stays visible and can be grabbed.
    private const double MinWidth = 0.03;

    // Wide enough to look sharp when printed, small enough not to make the Document much bigger.
    private const int SignaturePixels = 1200;

    public ImageFile? Signature => State.Signature;

    public IReadOnlyDictionary<int, Placement> Placements => State.Placements;

    public bool CanSave => Document is not null && State.Signature is not null && State.Placements.Count > 0;

    /// <summary>E.g. <c>Mietvertrag (unterschrieben).pdf</c>.</summary>
    public string? SuggestedFileName => Document is null ? null : $"{Document.NameWithoutExtension} (unterschrieben).pdf";

    /// <summary>
    /// Opens the Document to sign, replacing the current one, keeping the chosen signature: nothing
    /// to undo or save. Nothing is changed if the file cannot be opened.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public Task<LoadFailure?> OpenAsync(string filePath) =>
        OpenDocumentAsync(filePath, _ => Task.FromResult(State with { Placements = [] }));

    /// <summary>
    /// Takes this image as the signature, also where one is placed already. Nothing is changed if the
    /// file cannot be opened.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public async Task<LoadFailure?> ChooseSignatureAsync(string filePath)
    {
        var result = await ImageFile.LoadAllAsync([filePath]);
        if (result.Failures.Count > 0)
        {
            return result.Failures[0];
        }

        var signature = result.Images[0];
        Apply(new Signing(signature,
            State.Placements.ToImmutableDictionary(entry => entry.Key, entry => Fit(signature, entry.Key, entry.Value))));
        return null;
    }

    /// <summary>
    /// Starts with this image as the signature, e.g. the one chosen last time: not a change to undo.
    /// Only before anything else is chosen or placed; otherwise, and if the file can't be opened, nothing changes.
    /// </summary>
    public async Task OfferSignatureAsync(string filePath)
    {
        if (State.Signature is not null || CanUndo)
        {
            return;
        }

        var result = await ImageFile.LoadAllAsync([filePath]);
        if (result.Images.Count == 1 && State.Signature is null && !CanUndo)
        {
            Reset(State with { Signature = result.Images[0] });
        }
    }

    /// <summary>Puts the signature on the Page, bottom right, if it isn't on it yet.</summary>
    public void Sign(int pageIndex)
    {
        var signature = State.Signature!;
        if (State.Placements.ContainsKey(pageIndex))
        {
            return;
        }

        var heightPerWidth = HeightPerWidth(signature, pageIndex);
        var width = Math.Min(StartingWidth, StartingMaxHeight / heightPerWidth);
        var height = width * heightPerWidth;
        var placement = Fit(signature, pageIndex, new Placement(0.92 - width, 0.92 - height, width));
        Apply(State with { Placements = State.Placements.Add(pageIndex, placement) });
    }

    /// <summary>
    /// Moves and sizes the signature on the Page. It stays whole on the Page: a place partly
    /// outside is moved back in, and a width too big to fit is made smaller. No change if it is already there.
    /// </summary>
    public void Place(int pageIndex, Placement placement)
    {
        var fitted = Fit(State.Signature!, pageIndex, placement);
        if (fitted != State.Placements[pageIndex])
        {
            Apply(State with { Placements = State.Placements.SetItem(pageIndex, fitted) });
        }
    }

    /// <summary>Takes the signature off the Page.</summary>
    public void Unsign(int pageIndex)
    {
        if (State.Placements.ContainsKey(pageIndex))
        {
            Apply(State with { Placements = State.Placements.Remove(pageIndex) });
        }
    }

    /// <summary>The chosen signature as a PNG at most <paramref name="maxWidth"/> pixels wide, its paper transparent as it will be saved.</summary>
    public Task<byte[]> RenderSignaturePngAsync(int maxWidth) => State.Signature!.InkPngAsync(maxWidth);

    /// <summary>The signature's height as a share of the Page's height, at the given width share.</summary>
    public double HeightFor(int pageIndex, double width) => width * HeightPerWidth(State.Signature!, pageIndex);

    public async Task SaveAsync(string outputPath)
    {
        // Saved as it is now; later changes don't affect it.
        var (document, signing) = (Document!, State);
        var ink = await signing.Signature!.InkPngAsync(SignaturePixels);
        using var pdf = await document.OpenForChangesAsync();
        var content = await Task.Run(() =>
        {
            foreach (var (pageIndex, placement) in signing.Placements)
            {
                var page = pdf.Pages[pageIndex];
                var shown = ShownPage.Size(page);
                using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                graphics.MultiplyTransform(ShownPage.Transform(page));
                using var image = XImage.FromStream(new MemoryStream(ink, writable: false));
                var width = placement.Width * shown.Width;
                graphics.DrawImage(image, placement.Left * shown.Width, placement.Top * shown.Height,
                    width, width * signing.Signature.Height / signing.Signature.Width);
            }
            return Document.Save(pdf);
        });
        await File.WriteAllBytesAsync(outputPath, content);
        MarkSaved(signing);
    }

    /// <summary>The place nearest to <paramref name="placement"/> where the signature lies whole on the Page.</summary>
    private Placement Fit(ImageFile signature, int pageIndex, Placement placement)
    {
        var heightPerWidth = HeightPerWidth(signature, pageIndex);
        var maxWidth = Math.Min(1, 1 / heightPerWidth);
        var width = Math.Clamp(placement.Width, Math.Min(MinWidth, maxWidth), maxWidth);
        var height = width * heightPerWidth;
        return new Placement(Math.Clamp(placement.Left, 0, 1 - width), Math.Clamp(placement.Top, 0, 1 - height), width);
    }

    /// <summary>How many shares of the Page's height the signature takes per share of its width.</summary>
    private double HeightPerWidth(ImageFile signature, int pageIndex)
    {
        var page = Document!.ShownPageSize(pageIndex);
        return (double)signature.Height / signature.Width * page.Width / page.Height;
    }
}
