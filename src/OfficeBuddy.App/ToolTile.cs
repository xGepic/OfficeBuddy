using Microsoft.UI.Xaml;
using OfficeBuddy.App.Pages;

namespace OfficeBuddy.App;

/// <summary>What a Tool works on, which groups its tile on the start screen under a heading.</summary>
public enum ToolGroup
{
    /// <summary>Putting PDF Pages together, in order, or apart.</summary>
    PdfPages,

    /// <summary>Changing a whole PDF: smaller, signed, searchable, with or without a password, repaired.</summary>
    PdfFiles,

    /// <summary>From pictures to PDF and back.</summary>
    Convert,

    /// <summary>Photos and other pictures.</summary>
    Images,
}

/// <summary>The kind of file a Tool opens.</summary>
public enum FileKind
{
    Pdf,

    /// <summary>A JPG or PNG image.</summary>
    Image,
}

/// <summary>A heading above a group of tiles on the start screen.</summary>
public sealed record TileGroupHeader(string Title);

/// <summary>A tile on the start screen. Tiles without a page are Tools that are not built yet.</summary>
/// <param name="takesSeveral">Whether the Tool works on several files at once, rather than one.</param>
public sealed class ToolTile(string glyph, string resourceKey, ToolGroup group, Type? pageType, FileKind opens, bool takesSeveral = false)
{
    /// <summary>Names the Tool in the settings file.</summary>
    public string Id { get; } = resourceKey;

    public string Glyph { get; } = glyph;

    public ToolGroup Group { get; } = group;

    public string Name { get; } = Strings.Get($"Tool{resourceKey}Name");

    public string Description { get; } = Strings.Get($"Tool{resourceKey}Description");

    public Type? PageType { get; } = pageType;

    public bool IsAvailable => PageType is not null;

    /// <summary>Whether the Tool can work on these files, all of the same <paramref name="kind"/>.</summary>
    public bool Fits(FileKind kind, int count) => IsAvailable && opens == kind && (count == 1 || takesSeveral);

    public Visibility ComingSoonVisibility => IsAvailable ? Visibility.Collapsed : Visibility.Visible;

    public string ComingSoonText { get; } = Strings.Get("StartComingSoon");

    /// <summary>Every Tool, group by group in the order the start screen shows them.</summary>
    public static IReadOnlyList<ToolTile> All { get; } =
    [
        new("", "Merge", ToolGroup.PdfPages, typeof(MergePage), FileKind.Pdf, takesSeveral: true),
        new("", "EditPages", ToolGroup.PdfPages, typeof(EditPagesPage), FileKind.Pdf),
        new("", "ExtractPages", ToolGroup.PdfPages, typeof(ExtractPagesPage), FileKind.Pdf),
        new("", "Split", ToolGroup.PdfPages, typeof(SplitPage), FileKind.Pdf),

        new("", "Compress", ToolGroup.PdfFiles, typeof(CompressPage), FileKind.Pdf),
        new("", "Sign", ToolGroup.PdfFiles, typeof(SignPage), FileKind.Pdf),
        new("", "RecognizeText", ToolGroup.PdfFiles, typeof(RecognizeTextPage), FileKind.Pdf),
        new("", "PdfPassword", ToolGroup.PdfFiles, typeof(PdfPasswordPage), FileKind.Pdf),
        new("", "Repair", ToolGroup.PdfFiles, typeof(RepairPage), FileKind.Pdf),

        new("", "ImagesToPdf", ToolGroup.Convert, typeof(ImagesToPdfPage), FileKind.Image, takesSeveral: true),
        new("", "PageToImage", ToolGroup.Convert, typeof(PageToImagePage), FileKind.Pdf),

        new("", "ResizeImage", ToolGroup.Images, typeof(ResizeImagePage), FileKind.Image),
        new("", "CropImage", ToolGroup.Images, typeof(CropImagePage), FileKind.Image),
        new("", "PictureInPicture", ToolGroup.Images, typeof(PictureInPicturePage), FileKind.Image, takesSeveral: true),
        new("", "ImagesSideBySide", ToolGroup.Images, typeof(ImagesSideBySidePage), FileKind.Image, takesSeveral: true),
    ];
}
