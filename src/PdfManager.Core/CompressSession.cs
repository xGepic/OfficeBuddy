namespace PdfManager.Core;

/// <summary>
/// How far Compress has got: working on Page <paramref name="Page"/> of <paramref name="PageCount"/>
/// in its <paramref name="Step"/>th attempt (each stronger than the one before).
/// </summary>
public readonly record struct CompressProgress(int Step, int Page, int PageCount);

/// <summary>What Compress made of a Document: how big it was and is now.</summary>
public sealed record CompressResult(long OriginalSize, long CompressedSize, bool ReachedTarget)
{
    /// <summary>
    /// Whether Compress got the Document smaller at all. A digital Document that is lean already
    /// can come out a little bigger; that result is not worth saving.
    /// </summary>
    public bool IsSmaller => CompressedSize < OriginalSize;

    internal byte[] Content { get; init; } = [];
}

/// <summary>
/// The state of the Compress Tool: one Document, and once it has been compressed, the smaller
/// result. Compressing can be undone like any other change.
/// </summary>
public sealed class CompressSession() : DocumentSession<CompressResult?>(null)
{
    /// <summary>The Email size target: 10 MB, as File Explorer counts them.</summary>
    public const long EmailSizeTarget = 10L * 1024 * 1024;

    // Image resolutions (dpi) to try, lightest first. Below 96 dpi, text in scans becomes hard to read.
    private static readonly int[] Resolutions = [150, 120, 96];

    /// <summary>The size of the opened Document in bytes.</summary>
    public long OriginalSize => Document?.FileSize ?? 0;

    /// <summary>Whether the opened Document is under the Email size target already; Compress then only compresses lightly.</summary>
    public bool IsAlreadySmallEnough => Document is not null && OriginalSize < EmailSizeTarget;

    /// <summary>The compressed Document, or null if it has not been compressed (or that was undone).</summary>
    public CompressResult? Result => State;

    public bool CanCompress => Document is not null && State is null;

    public bool CanSave => State is { IsSmaller: true };

    /// <summary>E.g. <c>Rechnung (verkleinert).pdf</c>.</summary>
    public string? SuggestedFileName => Document is null ? null : $"{Document.NameWithoutExtension} (verkleinert).pdf";

    /// <summary>
    /// Opens the Document, replacing the current one, not yet compressed. Nothing is changed if the
    /// file cannot be opened.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public Task<LoadFailure?> OpenAsync(string filePath) =>
        OpenDocumentAsync(filePath, _ => Task.FromResult<CompressResult?>(null));

    /// <summary>
    /// Makes the Document smaller with ever smaller images until it is under the Email size target,
    /// as one undoable action. If even the smallest images don't get it there, the result is as
    /// small as Compress can make it.
    /// </summary>
    public async Task CompressAsync(IProgress<CompressProgress>? progress = null)
    {
        var document = Document!;
        var resolutions = IsAlreadySmallEnough ? Resolutions[..1] : Resolutions;
        var workFolder = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "PdfManager", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            const string original = "Original.pdf";
            await document.SaveCopyAsync(Path.Combine(workFolder, original));

            CompressResult? result = null;
            foreach (var (step, dpi) in resolutions.Index())
            {
                var compressed = $"{dpi} dpi.pdf";
                await Ghostscript.DownsampleImagesAsync(workFolder, original, compressed, dpi,
                    page => progress?.Report(new CompressProgress(step + 1, page, document.PageCount)));
                var content = await File.ReadAllBytesAsync(Path.Combine(workFolder, compressed));
                result = new CompressResult(OriginalSize, content.Length, content.Length < EmailSizeTarget) { Content = content };
                if (result.ReachedTarget)
                {
                    break;
                }
            }
            Apply(result);
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    public async Task SaveAsync(string outputPath)
    {
        var result = State!;
        await File.WriteAllBytesAsync(outputPath, result.Content);
        MarkSaved(result);
    }
}
