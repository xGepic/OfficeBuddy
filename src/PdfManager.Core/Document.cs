using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfManager.Core;

/// <summary>
/// A PDF file the user opened. Its content is read fully into memory, so the file on disk
/// can be overwritten when the result is saved.
/// </summary>
public sealed class Document
{
    private readonly byte[] _content;
    private readonly Lazy<Task<PageRenderer>> _renderer;

    private Document(string filePath, byte[] content, int pageCount)
    {
        FilePath = filePath;
        _content = content;
        PageCount = pageCount;
        _renderer = new(() => PageRenderer.OpenAsync(content));
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public string NameWithoutExtension => Path.GetFileNameWithoutExtension(FilePath);

    public string? Folder => Path.GetDirectoryName(FilePath);

    public int PageCount { get; }

    /// <summary>The size of the file in bytes, as it was opened.</summary>
    public long FileSize => _content.Length;

    /// <summary>
    /// Opens the files as Documents, in the given order. A file that cannot be opened is reported
    /// with the reason and does not stop the others.
    /// </summary>
    public static async Task<LoadResult> LoadAllAsync(IEnumerable<string> filePaths)
    {
        var documents = new List<Document>();
        var failures = new List<LoadFailure>();
        foreach (var filePath in filePaths)
        {
            try
            {
                documents.Add(await LoadAsync(filePath));
            }
            catch (LoadFailedException ex)
            {
                failures.Add(new LoadFailure(filePath, ex.Reason));
            }
        }
        return new LoadResult(documents, failures);
    }

    private static async Task<Document> LoadAsync(string filePath)
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

        if (!HasPdfHeader(content))
        {
            // Named .pdf but not one inside (an empty file, a saved error page): to the user it is a
            // PDF, so "only PDF files" would make no sense to them.
            throw new LoadFailedException(
                Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? LoadFailureReason.Damaged
                    : LoadFailureReason.Unsupported);
        }

        var passwordRequired = false;
        try
        {
            // Parsing a long Document takes a while; keep it off the caller's (UI) thread.
            var pageCount = await Task.Run(() =>
            {
                using var pdf = OpenForImport(content, args =>
                {
                    // Never try to get past the password: a Protected Document is refused.
                    passwordRequired = true;
                    args.Abort = true;
                });
                return pdf.PageCount;
            });
            return new Document(filePath, content, pageCount);
        }
        catch (Exception)
        {
            // PDFsharp throws all kinds of exceptions on a broken file; none of them reach the UI.
            throw new LoadFailedException(passwordRequired ? LoadFailureReason.Protected : LoadFailureReason.Damaged);
        }
    }

    /// <summary>
    /// Renders a Page (by its index in this Document) as a PNG image that fits inside the given
    /// pixel size, keeping its proportions and with its own rotation applied.
    /// </summary>
    public async Task<byte[]> RenderPageAsPngAsync(int pageIndex, int maxWidth, int maxHeight)
    {
        var renderer = await _renderer.Value;
        return await renderer.RenderPngAsync(pageIndex, maxWidth, maxHeight);
    }

    /// <summary>
    /// Renders a Page (by its index in this Document) as a JPG image at its real size and the given
    /// resolution, with its own rotation applied.
    /// </summary>
    public async Task<byte[]> RenderPageAsJpegAsync(int pageIndex, int dpi)
    {
        var renderer = await _renderer.Value;
        return await renderer.RenderJpegAsync(pageIndex, dpi);
    }

    /// <summary>Whether the file starts like a PDF. Readers accept the header anywhere in the first 1024 bytes.</summary>
    private static bool HasPdfHeader(byte[] content) =>
        content.AsSpan(0, Math.Min(content.Length, 1024)).IndexOf("%PDF-"u8) >= 0;

    /// <summary>Writes the file's content, as it was opened, to <paramref name="path"/>.</summary>
    internal Task SaveCopyAsync(string path) => File.WriteAllBytesAsync(path, _content);

    /// <summary>Opens a fresh PDFsharp copy whose Pages can be imported into a new Document.</summary>
    internal PdfDocument OpenForImport() => OpenForImport(_content, args => args.Abort = true);

    private static PdfDocument OpenForImport(byte[] content, PdfPasswordProvider passwordProvider) =>
        PdfReader.Open(new MemoryStream(content, writable: false), PdfDocumentOpenMode.Import, passwordProvider);

    private sealed class LoadFailedException(LoadFailureReason reason) : Exception
    {
        public LoadFailureReason Reason { get; } = reason;
    }
}

/// <summary>The Documents that were opened and the files that could not be opened.</summary>
public sealed record LoadResult(IReadOnlyList<Document> Documents, IReadOnlyList<LoadFailure> Failures);
