using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Pdf;

namespace OfficeBuddy.Core.Tools;

/// <summary>
/// The state of the PDF password Tool: one Document, opened with its password if it needs one,
/// and the password it will be saved with, or null for none. A Protected Document is saved without
/// its password, or with a new one; any other Document with a new password.
/// </summary>
public sealed class PdfPasswordSession() : ToolSession<string?>(null)
{
    public Document? Document { get; private set; }

    /// <summary>Whether the opened file needed a password to open.</summary>
    public bool WasProtected { get; private set; }

    /// <summary>The password the Document will be saved with, or null for none.</summary>
    public string? NewPassword => State;

    public bool CanSave => Document is not null && (WasProtected || State is not null);

    /// <summary>E.g. <c>Kontoauszug (ohne Passwort).pdf</c> or <c>Befund (mit Passwort).pdf</c>.</summary>
    public string? SuggestedFileName => Document is null
        ? null
        : $"{Document.NameWithoutExtension} ({(State is null ? "ohne Passwort" : "mit Passwort")}).pdf";

    public string? SuggestedFolder => Document?.Folder;

    /// <summary>
    /// Opens the Document, replacing the current one, to be saved without a password: nothing to
    /// undo. A Protected Document opens only with its right <paramref name="password"/>; without
    /// it, or with a wrong one, it is refused as Protected and nothing is changed.
    /// </summary>
    /// <returns>Why the file could not be opened, or null if it was.</returns>
    public async Task<LoadFailure?> OpenAsync(string filePath, string? password = null)
    {
        try
        {
            (Document, WasProtected) = await Document.LoadUnlockedAsync(filePath, password);
        }
        catch (LoadFailedException ex)
        {
            return new LoadFailure(filePath, ex.Reason);
        }

        Reset(null);
        return null;
    }

    /// <summary>Saves the Document with <paramref name="password"/>, or without one if it is null or empty.</summary>
    public void SetPassword(string? password)
    {
        var next = string.IsNullOrEmpty(password) ? null : password;
        if (next != State)
        {
            Apply(next);
        }
    }

    public async Task SaveAsync(string outputPath)
    {
        var (document, password) = (Document!, State);
        var content = password is null ? document.Content : await document.WithPasswordAsync(password);
        await File.WriteAllBytesAsync(outputPath, content);
        MarkSaved(password);
    }
}
