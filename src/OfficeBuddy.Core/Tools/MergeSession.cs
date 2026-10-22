using System.Collections.Immutable;
using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Pdf;

namespace OfficeBuddy.Core.Tools;

/// <summary>The state of the Merge Tool: whole Documents in the order they will be combined.</summary>
public sealed class MergeSession() : ToolSession<ImmutableList<Document>>([])
{
    public IReadOnlyList<Document> Documents => State;

    public bool CanSave => State.Count >= 2;

    public string? SuggestedFileName =>
        State.Count == 0 ? null : $"{State[0].NameWithoutExtension} (zusammengefügt).pdf";

    public string? SuggestedFolder => State.Count == 0 ? null : State[0].Folder;

    /// <summary>
    /// Adds the Documents that can be opened, in the given order. A file that cannot be opened
    /// does not stop the others.
    /// </summary>
    /// <returns>The files that could not be opened, and why.</returns>
    public async Task<IReadOnlyList<LoadFailure>> AddAsync(IEnumerable<string> filePaths)
    {
        var result = await Document.LoadAllAsync(filePaths);
        if (result.Documents.Count > 0)
        {
            Apply(State.AddRange(result.Documents));
        }
        return result.Failures;
    }

    public void Move(int from, int to) => Apply(State.RemoveAt(from).Insert(to, State[from]));

    public void Remove(int index) => Apply(State.RemoveAt(index));

    public async Task SaveAsync(string outputPath)
    {
        // Saved in the background from the list as it is now; later changes don't affect it.
        var documents = State;
        await DocumentWriter.WriteAsync(outputPath, [.. documents.SelectMany(document => Enumerable.Range(0, document.PageCount).Select(index => new PlannedPage(document, index)))]);
        MarkSaved(documents);
    }
}
