using System.Collections.Immutable;

namespace OfficeBuddy.Core;

/// <summary>
/// A session for Tools whose result is the Pages the user selects from one Document (Extract pages,
/// Page to image). Every change of the selection can be undone; the Document itself stays unchanged.
/// </summary>
public abstract class PageSelectionSession() : DocumentSession<ImmutableSortedSet<int>>([])
{
    /// <summary>All Pages of the Document, in their original order, for display.</summary>
    public IReadOnlyList<PageEntry> Pages => AllPages;

    /// <summary>The indexes of the selected Pages, in their original order.</summary>
    public IReadOnlyCollection<int> SelectedPageIndexes => State;

    public bool CanSave => Document is not null && State.Count > 0;

    /// <summary>
    /// Opens the Document, replacing the current one, with nothing selected. Nothing is changed if
    /// the file cannot be opened.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public Task<LoadFailure?> OpenAsync(string filePath) =>
        OpenDocumentAsync(filePath, _ => Task.FromResult(ImmutableSortedSet<int>.Empty));

    /// <summary>
    /// Makes exactly these Pages (by index) the selection, as one undoable action. The same
    /// selection again is not a change.
    /// </summary>
    public void SetSelection(IEnumerable<int> pageIndexes)
    {
        var selection = pageIndexes.ToImmutableSortedSet();
        if (!selection.SetEquals(State))
        {
            Apply(selection);
        }
    }
}
