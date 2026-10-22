using System.Text.Json;
using OfficeBuddy.Core.Tools;

namespace OfficeBuddy.App.Services;

/// <summary>
/// The user's settings, kept in a small JSON file under %LOCALAPPDATA%\Office Buddy, since the app
/// runs as a plain exe without package storage. Reading never fails: a missing or broken file
/// means the defaults.
/// </summary>
internal static class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Office Buddy", "settings.json");

    private static Stored? _stored;

    /// <summary>Ids of the Tools whose tiles the start screen shows first.</summary>
    public static IReadOnlySet<string> Favorites => Load().Favorites;

    public static void SetFavorite(string toolId, bool isFavorite)
    {
        var favorites = new HashSet<string>(Favorites);
        if (isFavorite ? favorites.Add(toolId) : favorites.Remove(toolId))
        {
            Save(Load() with { Favorites = favorites });
        }
    }

    /// <summary>The image of the user's signature chosen last in Sign, to offer again.</summary>
    public static string? SignaturePath => Load().SignaturePath;

    public static void SetSignaturePath(string path)
    {
        if (path != SignaturePath)
        {
            Save(Load() with { SignaturePath = path });
        }
    }

    /// <summary>Where the save dialog opens: next to the original unless the user chose otherwise.</summary>
    public static SaveLocation SaveLocation => Load().SaveLocation;

    /// <summary>The folder chosen for <see cref="SaveLocation.ChosenFolder"/>, if any.</summary>
    public static string? SaveFolder => Load().SaveFolder;

    public static void SetSaveLocation(SaveLocation location, string? folder = null) =>
        Save(Load() with { SaveLocation = location, SaveFolder = folder ?? SaveFolder });

    /// <summary>
    /// The folder the save dialog opens in for a result whose original is in
    /// <paramref name="originalFolder"/>. Back to the original's folder if the chosen one is gone,
    /// such as a USB stick that was pulled out.
    /// </summary>
    public static string? SaveFolderFor(string? originalFolder)
    {
        var folder = SaveLocation switch
        {
            SaveLocation.Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            SaveLocation.Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            SaveLocation.ChosenFolder => SaveFolder,
            _ => originalFolder,
        };
        return !string.IsNullOrEmpty(folder) && Directory.Exists(folder) ? folder : originalFolder;
    }

    /// <summary>The sizes an email attachment may have that the user can choose from, in megabytes.</summary>
    public static IReadOnlyList<int> EmailSizeLimits { get; } = [10, 20, 25];

    /// <summary>How big an email attachment may be with the user's email provider, in megabytes.</summary>
    public static int EmailSizeLimitMegabytes =>
        EmailSizeLimits.Contains(Load().EmailSizeLimitMegabytes) ? Load().EmailSizeLimitMegabytes : CompressSession.DefaultEmailSizeLimitMegabytes;

    public static void SetEmailSizeLimitMegabytes(int megabytes)
    {
        if (megabytes != EmailSizeLimitMegabytes)
        {
            Save(Load() with { EmailSizeLimitMegabytes = megabytes });
        }
    }

    private static Stored Load()
    {
        if (_stored is null)
        {
            try
            {
                _stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(FilePath));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
            }

            _stored ??= new Stored([]);
        }

        return _stored;
    }

    /// <summary>Keeps the settings for this session even if they cannot be written to disk.</summary>
    private static void Save(Stored stored)
    {
        _stored = stored;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(stored));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record Stored(
        HashSet<string> Favorites,
        string? SignaturePath = null,
        SaveLocation SaveLocation = SaveLocation.NextToOriginal,
        string? SaveFolder = null,
        int EmailSizeLimitMegabytes = CompressSession.DefaultEmailSizeLimitMegabytes);
}

/// <summary>Where the save dialog opens.</summary>
internal enum SaveLocation
{
    NextToOriginal,
    Documents,
    Desktop,
    ChosenFolder,
}
