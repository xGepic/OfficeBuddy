namespace OfficeBuddy.Core;

/// <summary>
/// How far Compress has got: working on Page <paramref name="Page"/> of <paramref name="PageCount"/>
/// in its <paramref name="Step"/>th attempt (each stronger than the one before), or, after the last
/// attempt, sorting the Pages into Parts that each fit an email (<paramref name="PlanningParts"/>).
/// </summary>
public readonly record struct CompressProgress(int Step, int Page, int PageCount, bool PlanningParts = false);

/// <summary>What Compress made of a Document: how big it was and is now.</summary>
public sealed record CompressResult(long OriginalSize, long CompressedSize, bool ReachedTarget)
{
    /// <summary>
    /// Whether Compress got the Document smaller at all. A digital Document that is lean already
    /// can come out a little bigger; that result is not worth saving.
    /// </summary>
    public bool IsSmaller => CompressedSize < OriginalSize;

    /// <summary>
    /// How many Parts that each fit an email the result can be saved as: 0 if it fits one email
    /// already, or can't be split because of <see cref="OversizedPageNumbers"/>.
    /// </summary>
    public int PartCount => Parts.Count;

    /// <summary>
    /// The numbers of the Pages that are too big for an email even on their own, which keeps the
    /// result from being split into Parts.
    /// </summary>
    public IReadOnlyList<int> OversizedPageNumbers { get; init; } = [];

    internal byte[] Content { get; init; } = [];

    internal IReadOnlyList<EmailPart> Parts { get; init; } = [];
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

    /// <summary>Whether the result, still too big for one email, can be saved as several Parts that each fit one.</summary>
    public bool CanSaveAsParts => State is { PartCount: > 0 };

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
            Path.Combine(Path.GetTempPath(), "OfficeBuddy", Guid.NewGuid().ToString("N"))).FullName;
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

            // A Document that fit an email before Compress needs no Parts, whatever the light compression made of it.
            if (!result!.ReachedTarget && !IsAlreadySmallEnough)
            {
                var compressed = Document.FromContent(document.FilePath, result.Content);
                var (parts, oversized) = await Task.Run(() => EmailParts.Plan(compressed, EmailSizeTarget,
                    page => progress?.Report(new CompressProgress(resolutions.Length, page, compressed.PageCount, PlanningParts: true))));
                // One Part would be the whole Document again, rebuilt just under the target: not worth calling a split.
                result = result with { Parts = parts.Count >= 2 ? parts : [], OversizedPageNumbers = oversized };
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

    /// <summary>The names of the files <see cref="SaveAsPartsAsync"/> writes with this base name, so existing ones can be checked first.</summary>
    public IReadOnlyList<string> PartFileNamesFor(string baseName) => PartFiles.Names(baseName, State?.PartCount ?? 0);

    /// <summary>
    /// Saves the result as Parts that each fit an email into <paramref name="folder"/>, all or nothing.
    /// </summary>
    /// <returns>The paths of the files written, Part by Part.</returns>
    public async Task<IReadOnlyList<string>> SaveAsPartsAsync(string folder, string baseName)
    {
        var result = State!;
        var written = await SeveralFiles.WriteAsync(folder, PartFileNamesFor(baseName), async (i, path) =>
        {
            await File.WriteAllBytesAsync(path, result.Parts[i].Content);
            // A Part over the target would bounce just like the whole Document.
            if (new FileInfo(path).Length >= EmailSizeTarget)
            {
                throw new InvalidOperationException($"Part {i + 1} came out over the Email size target.");
            }
        });
        MarkSaved(result);
        return written;
    }
}
