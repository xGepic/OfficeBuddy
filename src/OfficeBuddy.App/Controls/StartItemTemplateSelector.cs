using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OfficeBuddy.App.Controls;

/// <summary>Shows a group heading on the start screen as a heading, and anything else as a tile.</summary>
public sealed partial class StartItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Heading { get; set; }

    public DataTemplate? Tile { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item is TileGroupHeader ? Heading : Tile;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
