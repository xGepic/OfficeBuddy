using Microsoft.UI.Xaml;
using OfficeBuddy.App.Pages;

namespace OfficeBuddy.App;

/// <summary>A tile on the start screen. Tiles without a page are Tools that are not built yet.</summary>
public sealed class ToolTile(string glyph, string resourceKey, Type? pageType)
{
    /// <summary>Names the Tool in the settings file.</summary>
    public string Id { get; } = resourceKey;

    public string Glyph { get; } = glyph;

    public string Name { get; } = Strings.Get($"Tool{resourceKey}Name");

    public string Description { get; } = Strings.Get($"Tool{resourceKey}Description");

    public Type? PageType { get; } = pageType;

    public bool IsAvailable => PageType is not null;

    public Visibility ComingSoonVisibility => IsAvailable ? Visibility.Collapsed : Visibility.Visible;

    public string ComingSoonText { get; } = Strings.Get("StartComingSoon");

    public static IReadOnlyList<ToolTile> All { get; } =
    [
        new("", "Merge", typeof(MergePage)),
        new("", "EditPages", typeof(EditPagesPage)),
        new("", "ExtractPages", typeof(ExtractPagesPage)),
        new("", "Split", typeof(SplitPage)),
        new("", "Compress", typeof(CompressPage)),
        new("", "ImagesToPdf", typeof(ImagesToPdfPage)),
        new("", "PageToImage", typeof(PageToImagePage)),
        new("", "ResizeImage", typeof(ResizeImagePage)),
        new("", "PictureInPicture", typeof(PictureInPicturePage)),
        new("", "ImagesSideBySide", typeof(ImagesSideBySidePage)),
        new("", "CropImage", typeof(CropImagePage)),
    ];
}
