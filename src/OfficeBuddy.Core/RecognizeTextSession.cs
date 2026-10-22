using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace OfficeBuddy.Core;

/// <summary>A word Windows recognised on a Page, and its box, as shares of the Page as shown.</summary>
public sealed record RecognizedWord(string Text, double Left, double Top, double Width, double Height);

/// <summary>The lines of words recognised on one Page, top to bottom.</summary>
public sealed record RecognizedPage(IReadOnlyList<IReadOnlyList<RecognizedWord>> Lines)
{
    public string Text => string.Join(Environment.NewLine, Lines.Select(line => string.Join(" ", line.Select(word => word.Text))));

    public int WordCount => Lines.Sum(line => line.Count);
}

/// <summary>Told how many Pages have been read so far, out of how many.</summary>
public sealed record RecognizeProgress(int Done, int Total);

/// <summary>
/// The state of the Recognize text Tool: one Document, usually a scan, and the text Windows
/// recognised on its Pages. Saved, the text lies invisibly over each Page where it was seen, so the
/// Document looks the same but can be searched and its text selected and copied.
/// </summary>
public sealed class RecognizeTextSession() : DocumentSession<IReadOnlyList<RecognizedPage>?>(null)
{
    // Enough for small print; Windows reads images up to OcrEngine.MaxImageDimension pixels a side.
    private const int Dpi = 300;

    static RecognizeTextSession()
    {
        // Text needs a font; take Windows' own. PDFsharp allows setting this once, and not even
        // reading it first, so it is set blindly; it may already be set.
        try
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Whether Windows can recognise text on this PC: it needs a language with text recognition installed.</summary>
    public static bool IsAvailable => CreateEngine() is not null;

    /// <summary>What was recognised on each Page, or null before recognising.</summary>
    public IReadOnlyList<RecognizedPage>? Pages => State;

    /// <summary>All recognised text, Page after Page.</summary>
    public string Text => State is null ? "" : string.Join(Environment.NewLine + Environment.NewLine, State.Select(page => page.Text));

    public int WordCount => State?.Sum(page => page.WordCount) ?? 0;

    public bool CanSave => WordCount > 0;

    /// <summary>E.g. <c>Scan (durchsuchbar).pdf</c>.</summary>
    public string? SuggestedFileName => Document is null ? null : $"{Document.NameWithoutExtension} (durchsuchbar).pdf";

    /// <summary>
    /// Opens the Document, replacing the current one, with nothing recognised yet: nothing to undo or
    /// save. Nothing is changed if the file cannot be opened.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public Task<LoadFailure?> OpenAsync(string filePath) =>
        OpenDocumentAsync(filePath, _ => Task.FromResult<IReadOnlyList<RecognizedPage>?>(null));

    /// <summary>Reads the text on every Page, as one undoable action.</summary>
    /// <exception cref="InvalidOperationException">Text recognition isn't available on this PC (see <see cref="IsAvailable"/>).</exception>
    public async Task RecognizeAsync(IProgress<RecognizeProgress>? progress = null)
    {
        var document = Document!;
        var engine = CreateEngine() ?? throw new InvalidOperationException("Windows has no text recognition for any language on this PC.");
        var pages = new List<RecognizedPage>(document.PageCount);
        for (var index = 0; index < document.PageCount; index++)
        {
            progress?.Report(new RecognizeProgress(index, document.PageCount));
            pages.Add(await RecognizePageAsync(engine, document, index));
        }
        progress?.Report(new RecognizeProgress(document.PageCount, document.PageCount));
        Apply(pages);
    }

    public async Task SaveAsync(string outputPath)
    {
        // Saved as it is now; later changes don't affect it.
        var (document, pages) = (Document!, State!);
        using var pdf = await document.OpenForChangesAsync();
        var content = await Task.Run(() =>
        {
            for (var index = 0; index < pages.Count; index++)
            {
                if (pages[index].WordCount > 0)
                {
                    WriteInvisibly(pdf.Pages[index], pages[index]);
                }
            }
            return Document.Save(pdf);
        });
        await File.WriteAllBytesAsync(outputPath, content);
        MarkSaved(pages);
    }

    /// <summary>German first, as the app's users write it; else the languages of the user's Windows, else any there is.</summary>
    private static OcrEngine? CreateEngine() =>
        (OcrEngine.IsLanguageSupported(new Language("de-DE")) ? OcrEngine.TryCreateFromLanguage(new Language("de-DE")) : null)
        ?? OcrEngine.TryCreateFromUserProfileLanguages()
        ?? OcrEngine.AvailableRecognizerLanguages.Select(OcrEngine.TryCreateFromLanguage).FirstOrDefault(engine => engine is not null);

    private static async Task<RecognizedPage> RecognizePageAsync(OcrEngine engine, Document document, int index)
    {
        // A big Page at a lower resolution, so it fits what Windows can read.
        var size = document.ShownPageSize(index);
        var dpi = Math.Min(Dpi, (int)(OcrEngine.MaxImageDimension * 72 / Math.Max(size.Width, size.Height)));
        var jpeg = await document.RenderPageAsJpegAsync(index, dpi);

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(jpeg.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(bitmap);

        var (width, height) = ((double)bitmap.PixelWidth, (double)bitmap.PixelHeight);
        return new RecognizedPage([.. result.Lines.Select(line => (IReadOnlyList<RecognizedWord>)[.. line.Words.Select(word =>
            new RecognizedWord(word.Text, word.BoundingRect.X / width, word.BoundingRect.Y / height,
                word.BoundingRect.Width / width, word.BoundingRect.Height / height))])]);
    }

    /// <summary>
    /// Writes the words over the Page where they were seen, each stretched to its width, invisibly:
    /// there to search, select and copy, but not shown.
    /// </summary>
    private static void WriteInvisibly(PdfPage page, RecognizedPage recognized)
    {
        var shown = ShownPage.Size(page);
        using (var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
        {
            graphics.MultiplyTransform(ShownPage.Transform(page));
            foreach (var line in recognized.Lines)
            {
                // One size per line, from the top of its tallest letters to the bottom of its lowest,
                // so the words sit on one baseline; a word's own box depends on its letters.
                var top = line.Min(word => word.Top) * shown.Height;
                var bottom = line.Max(word => word.Top + word.Height) * shown.Height;
                var size = bottom - top;
                if (size <= 0)
                {
                    continue;
                }

                // Unicode, so umlauts and ß come out as they are.
                var font = new XFont("Arial", size, XFontStyleEx.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
                // Most lines reach below the baseline (g, p, comma); a fifth of the size is about how far.
                var baseline = bottom - 0.2 * size;
                foreach (var word in line)
                {
                    var natural = graphics.MeasureString(word.Text, font).Width;
                    if (natural <= 0 || word.Width <= 0)
                    {
                        continue;
                    }

                    var state = graphics.Save();
                    graphics.TranslateTransform(word.Left * shown.Width, baseline);
                    graphics.ScaleTransform(word.Width * shown.Width / natural, 1);
                    graphics.DrawString(word.Text, font, XBrushes.Black, 0, 0, XStringFormats.BaseLineLeft);
                    graphics.Restore(state);
                }
            }
        }
        MakeTextInvisible(page);
    }

    /// <summary>
    /// Switches the text just drawn in the Page's last content to PDF's invisible mode (render mode
    /// 3, as scanners' own text recognition uses), which PDFsharp can't do itself.
    /// </summary>
    private static void MakeTextInvisible(PdfPage page)
    {
        var contents = page.Contents.Elements;
        var drawn = (PdfDictionary)((PdfSharp.Pdf.Advanced.PdfReference)contents[^1]).Value;
        var commands = Encoding.Latin1.GetString(drawn.Stream.UnfilteredValue);
        // Every piece of text starts with BT; nothing else can contain it, as PDFsharp writes text as hex.
        var invisible = commands.Replace("BT\n", "BT\n3 Tr\n", StringComparison.Ordinal);
        if (invisible == commands)
        {
            throw new InvalidOperationException("PDFsharp wrote the text in an unexpected way.");
        }

        drawn.Elements.Remove("/Filter");
        drawn.Elements.Remove("/DecodeParms");
        drawn.Stream.Value = Encoding.Latin1.GetBytes(invisible);
    }
}
