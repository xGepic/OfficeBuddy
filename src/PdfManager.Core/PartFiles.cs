namespace PdfManager.Core;

/// <summary>File names for Parts saved together into one folder: <c>Rechnung - Teil 1.pdf</c>, <c>Rechnung - Teil 2.pdf</c>, …</summary>
internal static class PartFiles
{
    /// <summary>From 10 Parts on, numbers get leading zeros (Teil 01 … Teil 12) so the files sort in order.</summary>
    public static IReadOnlyList<string> Names(string baseName, int count)
    {
        var digits = count.ToString().Length;
        return Enumerable.Range(1, count)
            .Select(number => $"{baseName} - Teil {number.ToString().PadLeft(digits, '0')}.pdf")
            .ToList();
    }
}
