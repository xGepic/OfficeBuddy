using OfficeBuddy.Core.Pdf;

namespace OfficeBuddy.Core.Tools;

/// <summary>
/// The state of the Extract pages Tool: one Document and the Pages selected to be saved as a new
/// Document. The source Document stays unchanged.
/// </summary>
public sealed class ExtractPagesSession : PageSelectionSession
{
    /// <summary>E.g. <c>Rechnung (Seite 2).pdf</c> or <c>Rechnung (Seiten 1, 4-6).pdf</c>; null while nothing is selected.</summary>
    public string? SuggestedFileName => Document is null || State.Count == 0
        ? null
        : $"{Document.NameWithoutExtension} ({(State.Count == 1 ? "Seite" : "Seiten")} {DescribePages(State)}).pdf";

    public async Task SaveAsync(string outputPath)
    {
        var (document, selected) = (Document!, State);
        await DocumentWriter.WriteAsync(outputPath, [.. selected.Select(index => new PlannedPage(document, index))]);
        MarkSaved(selected);
    }

    /// <summary>Page numbers with runs of neighbours joined, e.g. "1, 4-6".</summary>
    private static string DescribePages(IEnumerable<int> pageIndexes)
    {
        var runs = new List<(int First, int Last)>();
        foreach (var number in pageIndexes.Select(index => index + 1))
        {
            if (runs.Count > 0 && runs[^1].Last == number - 1)
            {
                runs[^1] = (runs[^1].First, number);
            }
            else
            {
                runs.Add((number, number));
            }
        }
        return string.Join(", ", runs.Select(run => run.First == run.Last ? $"{run.First}" : $"{run.First}-{run.Last}"));
    }
}
