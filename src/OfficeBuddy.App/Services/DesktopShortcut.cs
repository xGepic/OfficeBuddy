using System.Runtime.InteropServices;

namespace OfficeBuddy.App.Services;

/// <summary>
/// Puts Office Buddy on the desktop for users who started the downloaded exe straight from their
/// Downloads folder, where it is easily lost or cleaned away: copies the exe to
/// %LOCALAPPDATA%\Programs\Office Buddy and puts a shortcut to that copy on the desktop.
/// </summary>
internal static class DesktopShortcut
{
    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");
    private static readonly Guid ShellLinkClsid = new("00021401-0000-0000-C000-000000000046");

    private static readonly string InstalledExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Office Buddy", "OfficeBuddy.exe");

    /// <summary>Whether this exe runs from the Downloads folder (or a folder in it).</summary>
    public static bool IsRunningFromDownloads
    {
        get
        {
            if (Environment.ProcessPath is not { } exe || DownloadsFolder() is not { } downloads)
            {
                return false;
            }

            var folder = Path.GetFullPath(Path.GetDirectoryName(exe)!);
            downloads = Path.GetFullPath(downloads).TrimEnd(Path.DirectorySeparatorChar);
            return folder.Equals(downloads, StringComparison.OrdinalIgnoreCase)
                || folder.StartsWith(downloads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Copies this exe to its own folder, replacing an older copy, and puts "Office Buddy" on the
    /// desktop pointing to it. Throws an <see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>
    /// or <see cref="COMException"/> if that fails, e.g. while the installed copy is running.
    /// </summary>
    public static void Create()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(InstalledExe)!);
        File.Copy(Environment.ProcessPath!, InstalledExe, overwrite: true);
        // The copy keeps the download's mark from the internet, which would make Windows warn on
        // every start from the desktop. The user has already chosen to run this app.
        try { File.Delete(InstalledExe + ":Zone.Identifier"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { }

        var link = (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(ShellLinkClsid, throwOnError: true)!)!;
        try
        {
            link.SetPath(InstalledExe);
            link.SetWorkingDirectory(Path.GetDirectoryName(InstalledExe)!);
            link.SetDescription(Strings.Get("DesktopShortcutDescription"));
            var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Office Buddy.lnk");
            ((IPersistFile)link).Save(shortcut, true);
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    }

    /// <summary>The Downloads folder where the user has put it, or null if Windows can't say.</summary>
    private static string? DownloadsFolder()
    {
        var id = DownloadsFolderId;
        if (SHGetKnownFolderPath(ref id, 0, 0, out var path) != 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(path);
        }
        finally
        {
            Marshal.FreeCoTaskMem(path);
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid folderId, uint flags, nint token, out nint path);

    // The vtable layouts below follow shobjidl_core.h and objidl.h exactly; method order matters.

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath(nint file, int fileLength, nint findData, uint flags);
        void GetIDList(out nint idList);
        void SetIDList(nint idList);
        void GetDescription(nint name, int nameLength);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory(nint directory, int directoryLength);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments(nint arguments, int argumentsLength);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out ushort hotkey);
        void SetHotkey(ushort hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation(nint iconPath, int iconPathLength, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
        void Resolve(nint owner, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }
}
