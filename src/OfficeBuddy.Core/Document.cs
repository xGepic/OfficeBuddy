using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;

namespace OfficeBuddy.Core;

/// <summary>
/// A PDF file the user opened. Its content is read fully into memory, so the file on disk
/// can be overwritten when the result is saved.
/// </summary>
public sealed class Document
{
    private readonly byte[] _content;
    private readonly Lazy<Task<PageRenderer>> _renderer;
    private readonly Lazy<IReadOnlyList<PageDimensions>> _shownPageSizes;

    private Document(string filePath, byte[] content, int pageCount)
    {
        FilePath = filePath;
        _content = content;
        PageCount = pageCount;
        _renderer = new(() => PageRenderer.OpenAsync(content));
        _shownPageSizes = new(() =>
        {
            using var pdf = OpenForImport();
            return [.. pdf.Pages.Cast<PdfPage>().Select(ShownPage.Size)];
        });
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
        var content = await ReadContentAsync(filePath);
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
    /// Reads a file that should be a PDF, fully into memory. Throws a <see cref="LoadFailedException"/>
    /// if it can't be read or isn't a PDF.
    /// </summary>
    internal static async Task<byte[]> ReadContentAsync(string filePath)
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
        return content;
    }

    /// <summary>
    /// Opens a file that may be a Protected Document. With its right <paramref name="password"/>
    /// it opens without the password, as if it never had one; without a password, or with a wrong
    /// one, it is refused as Protected. A file that needs no password opens as usual.
    /// </summary>
    /// <returns>The Document, and whether the file needed a password.</returns>
    internal static async Task<(Document Document, bool WasProtected)> LoadUnlockedAsync(string filePath, string? password)
    {
        var content = await ReadContentAsync(filePath);
        var passwordRequired = false;
        try
        {
            await Task.Run(() =>
            {
                using var pdf = OpenForImport(content, args =>
                {
                    passwordRequired = true;
                    args.Abort = true;
                });
            });
            return (FromContent(filePath, content), false);
        }
        catch (Exception) when (!passwordRequired)
        {
            throw new LoadFailedException(LoadFailureReason.Damaged);
        }
        catch (Exception)
        {
            // It needs a password.
        }

        if (password is null)
        {
            throw new LoadFailedException(LoadFailureReason.Protected);
        }

        try
        {
            await Task.Run(() =>
            {
                using var pdf = PdfReader.Open(new MemoryStream(content, writable: false), password, PdfDocumentOpenMode.Import);
            });
        }
        catch (Exception)
        {
            // A wrong password.
            throw new LoadFailedException(LoadFailureReason.Protected);
        }

        try
        {
            // PDFsharp takes the password off and leaves everything else as it is, but only with the
            // owner's password; with just the one to open the file, Ghostscript writes it anew.
            var unlocked = await Task.Run(() => RemovePassword(content, password)) ?? await Ghostscript.RewriteAsync(content, password);
            return (FromContent(filePath, unlocked), true);
        }
        catch (Exception)
        {
            throw new LoadFailedException(LoadFailureReason.Damaged);
        }
    }

    /// <summary>The content without its password, or null if <paramref name="password"/> isn't the owner's.</summary>
    private static byte[]? RemovePassword(byte[] content, string password)
    {
        try
        {
            using var pdf = PdfReader.Open(new MemoryStream(content, writable: false), password, PdfDocumentOpenMode.Modify);
            pdf.SecurityHandler.SetEncryption(PdfDefaultEncryption.None);
            return Save(pdf);
        }
        catch (PdfReaderException)
        {
            return null;
        }
    }

    /// <summary>
    /// A Document made by the app itself, such as a compressed one, under the name of the file it
    /// came from.
    /// </summary>
    internal static Document FromContent(string filePath, byte[] content)
    {
        using var pdf = OpenForImport(content, args => args.Abort = true);
        return new Document(filePath, content, pdf.PageCount);
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

    /// <summary>The size of a Page (by its index in this Document) as readers show it, turned by its own rotation.</summary>
    public PageDimensions ShownPageSize(int pageIndex) => _shownPageSizes.Value[pageIndex];

    /// <summary>Whether the file starts like a PDF. Readers accept the header anywhere in the first 1024 bytes.</summary>
    private static bool HasPdfHeader(byte[] content) =>
        content.AsSpan(0, Math.Min(content.Length, 1024)).IndexOf("%PDF-"u8) >= 0;

    /// <summary>Writes the file's content, as it was opened, to <paramref name="path"/>.</summary>
    internal Task SaveCopyAsync(string path) => File.WriteAllBytesAsync(path, _content);

    /// <summary>The file's content, as it was opened.</summary>
    internal byte[] Content => _content;

    /// <summary>Opens a fresh PDFsharp copy whose Pages can be imported into a new Document.</summary>
    internal PdfDocument OpenForImport() => OpenForImport(_content, args => args.Abort = true);

    /// <summary>
    /// Opens a fresh PDFsharp copy that can be changed and saved, e.g. to draw on its Pages. If the
    /// Document's owner has forbidden changes, Ghostscript writes it anew first, without that
    /// restriction: it is the user's own copy.
    /// </summary>
    internal async Task<PdfDocument> OpenForChangesAsync()
    {
        try
        {
            return await Task.Run(() => OpenForChanges(_content));
        }
        catch (Exception)
        {
            var rewritten = await Ghostscript.RewriteAsync(_content);
            return await Task.Run(() => OpenForChanges(rewritten));
        }
    }

    /// <summary>The Document as a Protected Document that opens only with <paramref name="password"/>, encrypted with AES-256.</summary>
    internal async Task<byte[]> WithPasswordAsync(string password)
    {
        using var pdf = await OpenForChangesAsync();
        return await Task.Run(() =>
        {
            // The same for owner and user: whoever knows it may do everything with the Document.
            pdf.SecuritySettings.UserPassword = password;
            pdf.SecuritySettings.OwnerPassword = password;
            pdf.SecurityHandler.SetEncryptionToV5();
            return Save(pdf);
        });
    }

    /// <summary>A changed PDFsharp copy, as the content of a PDF file.</summary>
    internal static byte[] Save(PdfDocument pdf)
    {
        using var buffer = new MemoryStream();
        pdf.Save(buffer);
        return buffer.ToArray();
    }

    private static PdfDocument OpenForChanges(byte[] content)
    {
        var passwordAsked = false;
        var pdf = PdfReader.Open(new MemoryStream(content, writable: false), PdfDocumentOpenMode.Modify, args =>
        {
            passwordAsked = true;
            args.Abort = true;
        });
        if (passwordAsked)
        {
            // Changes need the owner's password. PDFsharp still hands back a Document, without Pages.
            pdf.Dispose();
            throw new InvalidOperationException("Changing this Document needs its owner's password.");
        }
        return pdf;
    }

    private static PdfDocument OpenForImport(byte[] content, PdfPasswordProvider passwordProvider) =>
        PdfReader.Open(new MemoryStream(content, writable: false), PdfDocumentOpenMode.Import, passwordProvider);
}

/// <summary>The Documents that were opened and the files that could not be opened.</summary>
public sealed record LoadResult(IReadOnlyList<Document> Documents, IReadOnlyList<LoadFailure> Failures);
