using System.Diagnostics;

namespace OfficeBuddy.App.Services;

internal static class FileExplorer
{
    /// <summary>Opens File Explorer on the folder of <paramref name="path"/> with that file selected, ready to drag into an email.</summary>
    public static void ShowInFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Explorer would not start; the screen already says where the file is.
        }
    }
}
