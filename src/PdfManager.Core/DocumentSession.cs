namespace PdfManager.Core;

/// <summary>A Tool's session that works on one Document at a time (all Tools except Merge).</summary>
public abstract class DocumentSession<TState>(TState initial) : ToolSession<TState>(initial)
    where TState : class?
{
    public Document? Document { get; private set; }

    public string? SuggestedFolder => Document?.Folder;

    /// <summary>The start of the names of several saved files, e.g. <c>Rechnung</c> for <c>Rechnung - Teil 1.pdf</c>.</summary>
    public string? SuggestedBaseName => Document?.NameWithoutExtension;

    /// <summary>
    /// Opens the Document to work on, replacing the current one, and starts over from
    /// <paramref name="startingState"/>: nothing to undo or save. Nothing is changed if the file
    /// cannot be opened.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    protected async Task<LoadFailure?> OpenDocumentAsync(string filePath, Func<Document, Task<TState>> startingState)
    {
        var result = await Document.LoadAllAsync([filePath]);
        if (result.Failures.Count > 0)
        {
            return result.Failures[0];
        }

        var document = result.Documents[0];
        var state = await startingState(document);
        Document = document;
        Reset(state);
        return null;
    }

    /// <summary>Every Page of the Document in its original order, for Tools that don't change the Pages themselves.</summary>
    protected IReadOnlyList<PageEntry> AllPages =>
        Enumerable.Range(0, Document?.PageCount ?? 0).Select(index => new PageEntry(index)).ToList();
}
