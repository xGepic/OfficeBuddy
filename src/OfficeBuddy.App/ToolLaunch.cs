namespace OfficeBuddy.App;

/// <summary>
/// Navigation parameter for a Tool page: files to load straight away, e.g. dropped onto its tile.
/// For the start screen: files the app was started with, to choose a Tool for.
/// </summary>
public sealed record ToolLaunch(IReadOnlyList<string> FilePaths)
{
    /// <summary>The kind all the files are of, judged by their names; null if they are of different kinds or none the app opens.</summary>
    public FileKind? Kind
    {
        get
        {
            var kinds = FilePaths.Select(KindOf).Distinct().ToList();
            return kinds is [{ } kind] ? kind : null;
        }
    }

    private static FileKind? KindOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => FileKind.Pdf,
        ".jpg" or ".jpeg" or ".png" => FileKind.Image,
        _ => null,
    };
}
