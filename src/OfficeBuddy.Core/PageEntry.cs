namespace OfficeBuddy.Core;

/// <summary>
/// One Page as a Tool shows it and will save it: which Page of the source Document it is, its
/// pending rotation clockwise on top of its own (0, 90, 180 or 270 degrees), and whether it is
/// marked for removal.
/// </summary>
public sealed record PageEntry(int SourceIndex, int Rotation = 0, bool IsMarkedForRemoval = false);
