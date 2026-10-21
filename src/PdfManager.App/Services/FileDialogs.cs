using System.Runtime.InteropServices;

namespace PdfManager.App.Services;

/// <summary>
/// The standard Windows open/save dialogs. Used instead of the WinRT pickers because those
/// cannot open in a specific folder, which the save dialog needs (it starts next to the source).
/// </summary>
internal static class FileDialogs
{
    private const int ErrorCancelled = unchecked((int)0x800704C7);
    private static readonly Guid FileOpenDialogClsid = new("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");
    private static readonly Guid FileSaveDialogClsid = new("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B");

    public static IReadOnlyList<string> OpenFiles(nint owner, string title, FileFilter filter)
    {
        var dialog = (IFileOpenDialog)CreateInstance(FileOpenDialogClsid);
        try
        {
            dialog.SetTitle(title);
            dialog.SetFileTypes(1, [filter.ToSpec()]);
            dialog.GetOptions(out var options);
            dialog.SetOptions(options | Fos.ForceFileSystem | Fos.AllowMultiSelect | Fos.FileMustExist | Fos.NoChangeDir);

            if (ShowWasCancelled(dialog.Show(owner)))
            {
                return [];
            }

            dialog.GetResults(out var results);
            results.GetCount(out var count);
            var paths = new List<string>((int)count);
            for (uint i = 0; i < count; i++)
            {
                results.GetItemAt(i, out var item);
                paths.Add(FileSystemPath(item));
            }
            return paths;
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    /// <param name="askBeforeOverwriting">Off when the chosen name is only the start of the names actually written.</param>
    public static string? SaveFile(
        nint owner, string title, FileFilter filter, string? folder, string fileName, bool askBeforeOverwriting = true)
    {
        var dialog = (IFileDialog)CreateInstance(FileSaveDialogClsid);
        try
        {
            dialog.SetTitle(title);
            dialog.SetFileTypes(1, [filter.ToSpec()]);
            dialog.SetDefaultExtension(filter.DefaultExtension);
            dialog.SetFileName(fileName);
            dialog.GetOptions(out var options);
            var overwrite = askBeforeOverwriting ? Fos.OverwritePrompt : 0;
            dialog.SetOptions((options & ~Fos.OverwritePrompt) | Fos.ForceFileSystem | overwrite | Fos.PathMustExist | Fos.NoChangeDir);

            if (folder is not null && Directory.Exists(folder))
            {
                var iid = typeof(IShellItem).GUID;
                SHCreateItemFromParsingName(folder, 0, ref iid, out var folderItem);
                dialog.SetFolder(folderItem);
            }

            if (ShowWasCancelled(dialog.Show(owner)))
            {
                return null;
            }

            dialog.GetResult(out var result);
            return FileSystemPath(result);
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    /// <summary>True if the user cancelled; throws a <see cref="COMException"/> if the dialog itself failed.</summary>
    private static bool ShowWasCancelled(int hresult)
    {
        if (hresult == ErrorCancelled)
        {
            return true;
        }

        Marshal.ThrowExceptionForHR(hresult);
        return false;
    }

    private static object CreateInstance(Guid clsid) =>
        Activator.CreateInstance(Type.GetTypeFromCLSID(clsid, throwOnError: true)!)!;

    private static string FileSystemPath(IShellItem item)
    {
        item.GetDisplayName(Sigdn.FileSysPath, out var path);
        return path;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string path, nint bindContext, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

    [Flags]
    private enum Fos : uint
    {
        OverwritePrompt = 0x2,
        NoChangeDir = 0x8,
        ForceFileSystem = 0x40,
        AllowMultiSelect = 0x200,
        PathMustExist = 0x800,
        FileMustExist = 0x1000,
    }

    private enum Sigdn : uint
    {
        FileSysPath = 0x80058000,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct FilterSpec
    {
        public string Name;
        public string Spec;
    }

    // The vtable layouts below follow shobjidl_core.h exactly; method order matters.

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig] int Show(nint owner);
        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] filters);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(nint events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(Fos options);
        void GetOptions(out Fos options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, int place);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(nint filter);
    }

    [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(nint owner);
        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] filters);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(nint events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(Fos options);
        void GetOptions(out Fos options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, int place);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(nint filter);
        void GetResults(out IShellItemArray items);
        void GetSelectedItems(out IShellItemArray items);
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint bindContext, ref Guid handler, ref Guid riid, out nint result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(Sigdn type, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        void BindToHandler(nint bindContext, ref Guid handler, ref Guid riid, out nint result);
        void GetPropertyStore(int flags, ref Guid riid, out nint result);
        void GetPropertyDescriptionList(nint keyType, ref Guid riid, out nint result);
        void GetAttributes(int flags, uint mask, out uint attributes);
        void GetCount(out uint count);
        void GetItemAt(uint index, out IShellItem item);
        void EnumItems(out nint enumerator);
    }
}

/// <summary>A file type choice in a dialog, e.g. "PDF-Dokumente (*.pdf)".</summary>
internal sealed record FileFilter(string Name, string Pattern, string DefaultExtension)
{
    public static FileFilter Pdf => new(Strings.Get("PdfFilterName"), "*.pdf", "pdf");

    public static FileFilter Jpeg => new(Strings.Get("JpegFilterName"), "*.jpg", "jpg");

    internal FileDialogs.FilterSpec ToSpec() => new() { Name = Name, Spec = Pattern };
}
