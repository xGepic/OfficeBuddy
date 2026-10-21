namespace OfficeBuddy.App;

/// <summary>Navigation parameter for a Tool page: files to load straight away, e.g. dropped onto its tile.</summary>
public sealed record ToolLaunch(IReadOnlyList<string> FilePaths);
