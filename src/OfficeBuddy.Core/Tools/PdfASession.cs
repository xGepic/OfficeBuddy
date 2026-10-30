using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Pdf;

namespace OfficeBuddy.Core.Tools;

/// <summary>
/// The state of the PDF/A Tool: one Document, saved as PDF/A-2b, the archive format offices and
/// upload portals ask for. Nothing about it is changed on screen, so there is nothing to undo.
/// </summary>
public sealed class PdfASession() : DocumentSession<object?>(null)
{
    public bool CanSave => Document is not null;

    /// <summary>E.g. <c>Antrag (PDF-A).pdf</c>; a file name can't hold the slash of "PDF/A".</summary>
    public string? SuggestedFileName => Document is null ? null : $"{Document.NameWithoutExtension} (PDF-A).pdf";

    /// <summary>The Pages to show, all in their original order.</summary>
    public IReadOnlyList<PageEntry> Pages => AllPages;

    /// <summary>Opens the Document, replacing the current one. Nothing is changed if the file cannot be opened.</summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public Task<LoadFailure?> OpenAsync(string filePath) => OpenDocumentAsync(filePath, _ => Task.FromResult<object?>(null));

    /// <summary>Writes the Document as PDF/A to <paramref name="outputPath"/>.</summary>
    /// <exception cref="InvalidOperationException">Ghostscript could not convert it.</exception>
    public async Task SaveAsync(string outputPath)
    {
        var converted = await Ghostscript.ConvertToPdfAAsync(Document!.Content);
        await File.WriteAllBytesAsync(outputPath, converted);
        MarkSaved(State);
    }
}
