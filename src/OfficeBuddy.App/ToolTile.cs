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

/// <summary>A heading above a group of tiles on the start screen.</summary>
public sealed record TileGroupHeader(string Title);

/// <summary>A tile on the start screen. Tiles without a page are Tools that are not built yet.</summary>
public sealed class ToolTile(string glyph, string resourceKey, ToolGroup group, Type? pageType)
{
    /// <summary>Names the Tool in the settings file.</summary>
    public string Id { get; } = resourceKey;

    public string Glyph { get; } = glyph;

    public ToolGroup Group { get; } = group;

    public string Name { get; } = Strings.Get($"Tool{resourceKey}Name");

    public string Description { get; } = Strings.Get($"Tool{resourceKey}Description");

    public Type? PageType { get; } = pageType;

    public bool IsAvailable => PageType is not null;

    public Visibility ComingSoonVisibility => IsAvailable ? Visibility.Collapsed : Visibility.Visible;

    public string ComingSoonText { get; } = Strings.Get("StartComingSoon");

    /// <summary>Every Tool, group by group in the order the start screen shows them.</summary>
    public static IReadOnlyList<ToolTile> All { get; } =
    [
        new("", "Merge", ToolGroup.PdfPages, typeof(MergePage)),
        new("", "EditPages", ToolGroup.PdfPages, typeof(EditPagesPage)),
        new("", "ExtractPages", ToolGroup.PdfPages, typeof(ExtractPagesPage)),
        new("", "Split", ToolGroup.PdfPages, typeof(SplitPage)),

        new("", "Compress", ToolGroup.PdfFiles, typeof(CompressPage)),
        new("", "Sign", ToolGroup.PdfFiles, typeof(SignPage)),
        new("", "RecognizeText", ToolGroup.PdfFiles, typeof(RecognizeTextPage)),
        new("", "PdfPassword", ToolGroup.PdfFiles, typeof(PdfPasswordPage)),
        new("", "Repair", ToolGroup.PdfFiles, typeof(RepairPage)),

        new("", "ImagesToPdf", ToolGroup.Convert, typeof(ImagesToPdfPage)),
        new("", "PageToImage", ToolGroup.Convert, typeof(PageToImagePage)),

        new("", "ResizeImage", ToolGroup.Images, typeof(ResizeImagePage)),
        new("", "CropImage", ToolGroup.Images, typeof(CropImagePage)),
        new("", "PictureInPicture", ToolGroup.Images, typeof(PictureInPicturePage)),
        new("", "ImagesSideBySide", ToolGroup.Images, typeof(ImagesSideBySidePage)),
    ];
}
