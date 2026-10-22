using System.Text.Json;

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

    private sealed record Stored(HashSet<string> Favorites, string? SignaturePath = null);
}
