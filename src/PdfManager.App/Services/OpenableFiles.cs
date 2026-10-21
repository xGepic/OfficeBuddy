namespace PdfManager.App.Services;

/// <summary>
/// What a Tool opens, for its Öffnen dialog and for explaining files it can't use: PDF Documents, or
/// images for Images to PDF.
/// </summary>
internal sealed record OpenableFiles(FileFilter Filter, string DialogTitleKey, string UnsupportedOneKey, string UnsupportedManyKey)
{
    public static OpenableFiles Documents =>
        new(FileFilter.Pdf, "OpenDialogTitle", "LoadFailedUnsupportedOne", "LoadFailedUnsupportedMany");

    public static OpenableFiles Images =>
        new(FileFilter.Images, "OpenImagesDialogTitle", "LoadFailedNotAnImageOne", "LoadFailedNotAnImageMany");
}
