using System.Collections.Immutable;

namespace PdfManager.Core;

/// <summary>The state of the Edit pages Tool: the Pages of one Document as they will be saved.</summary>
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
            DetectedBlankPageNumbers = blankIndexes.Select(index => index + 1).ToList();
            return Enumerable.Range(0, document.PageCount)
                .Select(index => new PageEntry(index, IsMarkedForRemoval: blankIndexes.Contains(index)))
                .ToImmutableList();
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

    public async Task SaveAsync(string outputPath)
    {
        var (document, pages) = (Document!, State);
        await DocumentWriter.WriteAsync(outputPath, pages
            .Where(page => !page.IsMarkedForRemoval)
            .Select(page => new PlannedPage(document, page.SourceIndex, page.Rotation))
            .ToList());
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
