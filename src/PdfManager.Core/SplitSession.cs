using System.Collections.Immutable;

namespace PdfManager.Core;

/// <summary>Consecutive Pages, by index from <paramref name="First"/> to <paramref name="Last"/> inclusive.</summary>
public readonly record struct PageRange(int First, int Last);

/// <summary>
/// The state of the Split Tool: one Document and the Split points that cut it into Parts, or
/// every Page as its own Part.
/// </summary>
public sealed class SplitSession() : DocumentSession<SplitSession.Plan>(new Plan([], false))
{
    /// <summary>
    /// A Split point is the index of the Page that starts a new Part. With
    /// <paramref name="EachPageSeparately"/>, the Split points are kept but not used.
    /// </summary>
    public sealed record Plan(ImmutableSortedSet<int> SplitPoints, bool EachPageSeparately);

    /// <summary>All Pages of the Document, in their original order, for display.</summary>
    public IReadOnlyList<PageEntry> Pages => AllPages;

    /// <summary>The Parts the Document will be cut into, in order.</summary>
    public IReadOnlyList<PageRange> Parts
    {
        get
        {
            if (Document is null)
            {
                return [];
            }

            var starts = State.EachPageSeparately
                ? Enumerable.Range(0, Document.PageCount).ToList()
                : State.SplitPoints.Prepend(0).ToList();
            return starts
                .Select((start, i) => new PageRange(start, i + 1 < starts.Count ? starts[i + 1] - 1 : Document.PageCount - 1))
                .ToList();
        }
    }

    /// <summary>Splitting only makes sense with at least two Parts.</summary>
    public bool CanSave => Parts.Count >= 2;

    /// <summary>The Split points, as indexes of the Pages that start a new Part.</summary>
    public IReadOnlySet<int> SplitPoints => State.SplitPoints;

    public bool EachPageSeparately => State.EachPageSeparately;

    /// <summary>
    /// Opens the Document to split, replacing the current one, with no Split points. Nothing is
    /// changed if the file cannot be opened.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public Task<LoadFailure?> OpenAsync(string filePath) =>
        OpenDocumentAsync(filePath, _ => Task.FromResult(new Plan([], false)));

    /// <summary>Places a Split point before this Page (by index), or removes the one that is there.</summary>
    public void ToggleSplitPoint(int pageIndex)
    {
        // Between two Pages: never before the first Page or after the last.
        ArgumentOutOfRangeException.ThrowIfLessThan(pageIndex, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, Document?.PageCount ?? 0);
        var points = State.SplitPoints;
        Apply(State with { SplitPoints = points.Contains(pageIndex) ? points.Remove(pageIndex) : points.Add(pageIndex) });
    }

    /// <summary>Turns "each Page separately" on or off.</summary>
    public void SetEachPageSeparately(bool on) => Apply(State with { EachPageSeparately = on });

    /// <summary>The names of the files a save with this base name writes, so existing ones can be checked first.</summary>
    public IReadOnlyList<string> FileNamesFor(string baseName) => PartFiles.Names(baseName, Parts.Count);

    /// <summary>
    /// Saves every Part as its own Document into <paramref name="folder"/>, all or nothing: if one
    /// can't be written, no Part is left behind and files already there are unchanged.
    /// </summary>
    /// <returns>The paths of the files written, Part by Part.</returns>
    public async Task<IReadOnlyList<string>> SaveAsync(string folder, string baseName)
    {
        var (document, plan, parts) = (Document!, State, Parts);
        var written = await SeveralFiles.WriteAsync(folder, FileNamesFor(baseName), (i, path) =>
            DocumentWriter.WriteAsync(path, Enumerable.Range(parts[i].First, parts[i].Last - parts[i].First + 1)
                .Select(index => new PlannedPage(document, index))
                .ToList()));
        MarkSaved(plan);
        return written;
    }
}
