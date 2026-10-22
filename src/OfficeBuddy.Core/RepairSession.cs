namespace OfficeBuddy.Core;

/// <summary>A Document as Repair opened it: rebuilt from a damaged file, or the file itself if it wasn't damaged.</summary>
public sealed record RepairResult(Document Document, bool WasDamaged);

/// <summary>
/// The state of the Repair Tool: one PDF file that may be damaged, rebuilt by Ghostscript into a
/// Document that can be opened again, as far as anything of it can be saved.
/// </summary>
public sealed class RepairSession() : ToolSession<RepairResult?>(null)
{
    public RepairResult? Result => State;

    /// <summary>Saving is worth it only for a file that was damaged and could be rebuilt.</summary>
    public bool CanSave => State is { WasDamaged: true };

    /// <summary>E.g. <c>Rechnung (repariert).pdf</c>.</summary>
    public string? SuggestedFileName => State is null ? null : $"{State.Document.NameWithoutExtension} (repariert).pdf";

    public string? SuggestedFolder => State?.Document.Folder;

    /// <summary>
    /// Opens the file, replacing the current one, and rebuilds it if it is damaged: the starting
    /// point, not a change to undo. A Protected Document is refused, and so is a damaged file
    /// nothing could be saved from; then nothing is changed.
    /// </summary>
    /// <returns>Why the file could not be opened or rebuilt, or null if it was.</returns>
    public async Task<LoadFailure?> OpenAsync(string filePath)
    {
        var opened = await Document.LoadAllAsync([filePath]);
        if (opened.Documents.Count == 1)
        {
            Reset(new RepairResult(opened.Documents[0], WasDamaged: false));
            return null;
        }

        var failure = opened.Failures[0];
        if (failure.Reason != LoadFailureReason.Damaged)
        {
            return failure;
        }

        try
        {
            var content = await Document.ReadContentAsync(filePath);
            var rebuilt = await Ghostscript.RewriteAsync(content);
            var document = await Task.Run(() => Document.FromContent(filePath, rebuilt));
            if (document.PageCount == 0)
            {
                return failure;
            }

            Reset(new RepairResult(document, WasDamaged: true));
            return null;
        }
        catch (Exception)
        {
            // Unreadable, or nothing Ghostscript could make sense of: PDFsharp throws all kinds of
            // exceptions on what Ghostscript wrote from it, and none of them reach the UI.
            return failure;
        }
    }

    public async Task SaveAsync(string outputPath)
    {
        var result = State!;
        await result.Document.SaveCopyAsync(outputPath);
        MarkSaved(result);
    }
}
