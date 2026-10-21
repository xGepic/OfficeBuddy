namespace OfficeBuddy.Core;

/// <summary>One Part of a Document that fits in one email: its Pages and the Part as a PDF file.</summary>
internal sealed record EmailPart(PageRange Pages, byte[] Content);

/// <summary>
/// Splits a Document that is too big for one email into Parts that each fit one: Pages are added to
/// a Part until the next one would take it over the Email size target. Every Part is measured as
/// the real file it becomes, not estimated.
/// </summary>
internal static class EmailParts
{
    /// <param name="pageDone">Told the number of each Page once it has found its Part.</param>
    /// <returns>
    /// The Parts in page order; or, if some Pages are too big for an email on their own, no Parts
    /// and the numbers of those Pages.
    /// </returns>
    public static (IReadOnlyList<EmailPart> Parts, IReadOnlyList<int> OversizedPageNumbers) Plan(
        Document document, long target, Action<int>? pageDone)
    {
        var parts = new List<EmailPart>();
        var oversizedPageNumbers = new List<int>();
        EmailPart? current = null;
        for (var index = 0; index < document.PageCount; index++)
        {
            if (current is not null && Build(document, current.Pages with { Last = index }) is { } grown && grown.Length < target)
            {
                current = new EmailPart(current.Pages with { Last = index }, grown);
            }
            else
            {
                if (current is not null)
                {
                    parts.Add(current);
                }

                var alone = new PageRange(index, index);
                var aloneContent = Build(document, alone);
                current = aloneContent.Length < target ? new EmailPart(alone, aloneContent) : null;
                if (current is null)
                {
                    oversizedPageNumbers.Add(index + 1);
                }
            }
            pageDone?.Invoke(index + 1);
        }

        if (current is not null)
        {
            parts.Add(current);
        }
        return oversizedPageNumbers.Count > 0 ? ([], oversizedPageNumbers) : (parts, []);
    }

    private static byte[] Build(Document document, PageRange pages) =>
        DocumentWriter.Build(pages.Indexes.Select(index => new PlannedPage(document, index)));
}
