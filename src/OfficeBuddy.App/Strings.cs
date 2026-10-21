using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace OfficeBuddy.App;

/// <summary>German UI strings from the app's single resource file, for use in code.</summary>
internal static class Strings
{
    private static readonly ResourceLoader Loader = new();
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static string Get(string key) => Loader.GetString(key);

    public static string Format(string key, params object[] args) => string.Format(German, Get(key), args);

    /// <summary>"1 Seite ausgewählt", "3 Seiten ausgewählt", or a reminder to select one.</summary>
    public static string PagesSelected(int count) => count switch
    {
        0 => Get("PagesNoneSelected"),
        1 => Get("PagesSelectedOne"),
        _ => Format("PagesSelectedMany", count),
    };

    /// <summary>"1 Seite" or "3 Seiten".</summary>
    public static string PageCount(int count) => count == 1 ? Get("PageCountOne") : Format("PageCountMany", count);

    /// <summary>
    /// A file size the way File Explorer counts it, e.g. "23,4 MB" or "380 KB". Rounded down, so a
    /// Document just under the Email size target never shows as "10,0 MB".
    /// </summary>
    public static string FileSize(long bytes) => bytes < 1024 * 1024
        ? Format("SizeKilobytes", Math.Max(1, bytes / 1024))
        : Format("SizeMegabytes", Math.Floor(bytes * 10.0 / (1024 * 1024)) / 10);
}
