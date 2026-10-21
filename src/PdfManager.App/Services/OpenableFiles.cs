namespace PdfManager.App.Services;

/// <summary>
/// What a Tool opens, for its Öffnen dialog, for explaining files it can't use, and for Tools that
/// work on one file at a time: PDF Documents, or images for Images to PDF and Resize image.
/// </summary>
internal sealed record OpenableFiles(
    FileFilter Filter, string DialogTitleKey, string UnsupportedOneKey, string UnsupportedManyKey, string BusyOpeningOneKey,
    string OneOnlyKey)
{
    public static OpenableFiles Documents =>
        new(FileFilter.Pdf, "OpenDialogTitle", "LoadFailedUnsupportedOne", "LoadFailedUnsupportedMany", "BusyOpening",
            "OneDocumentOnly");

    public static OpenableFiles Images =>
        new(FileFilter.Images, "OpenImagesDialogTitle", "LoadFailedNotAnImageOne", "LoadFailedNotAnImageMany", "BusyOpeningImage",
            "OneImageOnly");
}
