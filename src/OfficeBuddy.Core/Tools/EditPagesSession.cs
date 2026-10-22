using System.Collections.Immutable;
using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Pdf;

namespace OfficeBuddy.Core.Tools;

/// <summary>
/// The state of the Edit pages Tool: the Pages of one Document as they will be saved, including
/// Pages inserted from other Documents.
/// </summary>
public sealed class EditPagesSession() : DocumentSession<ImmutableList<PageEntry>>([])
{
    public IReadOnlyList<PageEntry> Pages => State;

    /// <summary>True when saving would produce an empty Document, so the screen can explain why it can't save.</summary>
    public bool EveryPageMarkedForRemoval => State.Count > 0 && State.All(page => page.IsMarkedForRemoval);

    public bool CanSave => Document is not null && !EveryPageMarkedForRemoval;

    public string? SuggestedFileName => Document is null ? null : $"{Document.NameWithoutExtension} (bearbeitet).pdf";


    /// <summary>
    /// The page numbers of the Pages found blank when the Document was opened, which start out
    /// marked for removal.
    /// </summary>
    public IReadOnlyList<int> DetectedBlankPageNumbers { get; private set; } = [];

    /// <summary>
    /// Opens the Document to work on, replacing the current one, and marks its Blank pages for
    /// removal. That is the starting point, not a change to undo. Nothing is changed if the file
    /// cannot be opened.
    /// </summary>
    /// <param name="progress">Told how many Pages have been checked for being blank so far.</param>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public Task<LoadFailure?> OpenAsync(string filePath, IProgress<BlankPageProgress>? progress = null) =>
        OpenDocumentAsync(filePath, async document =>
        {
            // Looking at every Page takes a while; keep it off the caller's (UI) thread.
            var blankIndexes = await Task.Run(() => BlankPageDetection.FindAsync(document, progress));
            DetectedBlankPageNumbers = [.. blankIndexes.Select(index => index + 1)];
            return [.. Enumerable.Range(0, document.PageCount).Select(index => new PageEntry(index, IsMarkedForRemoval: blankIndexes.Contains(index)))];
        });

    /// <summary>Marks the Pages at these positions in <see cref="Pages"/> to be left out when saving.</summary>
    public void MarkForRemoval(IEnumerable<int> positions) =>
        Update(positions, page => page with { IsMarkedForRemoval = true });

    /// <summary>Whether every one of these Pages is marked, so the action on them is to keep them after all.</summary>
    public bool AreAllMarkedForRemoval(IReadOnlyCollection<int> positions) =>
        positions.Count > 0 && positions.All(position => State[position].IsMarkedForRemoval);

    /// <summary>Keeps the Pages at these positions after all.</summary>
    public void Unmark(IEnumerable<int> positions) =>
        Update(positions, page => page with { IsMarkedForRemoval = false });

    /// <summary>Rotates the Pages at these positions 90° clockwise.</summary>
    public void RotateRight(IEnumerable<int> positions) =>
        Update(positions, page => page with { Rotation = (page.Rotation + 90) % 360 });

    /// <summary>Rotates the Pages at these positions 90° counter-clockwise.</summary>
    public void RotateLeft(IEnumerable<int> positions) =>
        Update(positions, page => page with { Rotation = (page.Rotation + 270) % 360 });

    public void Move(int from, int to) => Apply(State.RemoveAt(from).Insert(to, State[from]));

    /// <summary>
    /// Inserts every Page of the Documents, in the given order, at <paramref name="position"/> in
    /// <see cref="Pages"/>, kept and not rotated, as one undoable action. A file that cannot be opened
    /// does not stop the others.
    /// </summary>
    /// <returns>The files that could not be opened, and why.</returns>
    public async Task<IReadOnlyList<LoadFailure>> InsertAsync(IEnumerable<string> filePaths, int position)
    {
        var result = await Document.LoadAllAsync(filePaths);
        var inserted = result.Documents
            .SelectMany(document => Enumerable.Range(0, document.PageCount).Select(index => new PageEntry(index, InsertedFrom: document)))
            .ToList();
        if (inserted.Count > 0)
        {
            Apply(State.InsertRange(position, inserted));
        }
        return result.Failures;
    }

    public async Task SaveAsync(string outputPath)
    {
        var (document, pages) = (Document!, State);
        await DocumentWriter.WriteAsync(outputPath, [.. pages
            .Where(page => !page.IsMarkedForRemoval)
            .Select(page => new PlannedPage(page.InsertedFrom ?? document, page.SourceIndex, page.Rotation))]);
        MarkSaved(pages);
    }

    /// <summary>Changes the Pages at <paramref name="positions"/>, as one undoable action.</summary>
    private void Update(IEnumerable<int> positions, Func<PageEntry, PageEntry> change)
    {
        var pages = State.ToBuilder();
        foreach (var position in positions)
        {
            pages[position] = change(pages[position]);
        }
        Apply(pages.ToImmutable());
    }
}
