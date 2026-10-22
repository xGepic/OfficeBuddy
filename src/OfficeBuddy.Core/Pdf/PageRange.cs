namespace OfficeBuddy.Core.Pdf;

/// <summary>Consecutive Pages, by index from <paramref name="First"/> to <paramref name="Last"/> inclusive.</summary>
public readonly record struct PageRange(int First, int Last)
{
    internal IEnumerable<int> Indexes => Enumerable.Range(First, Last - First + 1);
}
