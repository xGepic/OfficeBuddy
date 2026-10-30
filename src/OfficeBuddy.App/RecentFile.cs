namespace OfficeBuddy.App;

/// <summary>A file the app saved lately, which the start screen offers to work on further.</summary>
public sealed class RecentFile(string filePath)
{
    public string FilePath { get; } = filePath;

    public string Name { get; } = Path.GetFileName(filePath);

    /// <summary>The name of the folder the file is in, such as "Downloads"; the whole path is in <see cref="FilePath"/>.</summary>
    public string FolderName { get; } = Path.GetFileName(Path.GetDirectoryName(filePath)) is { Length: > 0 } name
        ? name
        : Path.GetDirectoryName(filePath) ?? "";

    /// <summary>A picture for images, a document for PDFs.</summary>
    public string Glyph { get; } = ToolLaunch.KindOf(filePath) == FileKind.Image ? "" : "";

    public string OpenLabel { get; } = Strings.Format("StartRecentOpen", Path.GetFileName(filePath));

    public string ShowInFolderLabel { get; } = Strings.Get("ShowInFolder");
}
