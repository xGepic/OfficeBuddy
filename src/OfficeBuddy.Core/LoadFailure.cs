namespace OfficeBuddy.Core;

/// <summary>Why a file could not be opened as a Document.</summary>
public enum LoadFailureReason
{
    /// <summary>A Protected Document: it needs a password to open.</summary>
    Protected,

    /// <summary>A damaged PDF, or a file that could not be read at all.</summary>
    Damaged,

    /// <summary>Not a PDF.</summary>
    Unsupported,
}

/// <summary>Thrown while opening a file that can't be used, and caught where the files are opened.</summary>
internal sealed class LoadFailedException(LoadFailureReason reason) : Exception
{
    public LoadFailureReason Reason { get; } = reason;
}

/// <summary>A file the user tried to open that was not added, and why.</summary>
public sealed record LoadFailure(string FilePath, LoadFailureReason Reason)
{
    public string FileName => Path.GetFileName(FilePath);
}
