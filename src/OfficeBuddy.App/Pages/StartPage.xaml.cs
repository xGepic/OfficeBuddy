using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OfficeBuddy.App.Controls;
using OfficeBuddy.App.Services;

namespace OfficeBuddy.App.Pages;

public sealed partial class StartPage : Page
{
    public StartPage()
    {
        InitializeComponent();
    }

    public IReadOnlyList<ToolTile> Tiles => ToolTile.All;

    /// <summary>Tells the tile grid how much height is left below the header, so all tiles fit without scrolling.</summary>
    private void Layout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (TileList.ItemsPanelRoot is TileGrid grid)
        {
            var padding = PageContent.Padding;
            grid.FitHeight = Scroller.ViewportHeight - padding.Top - padding.Bottom
                - Header.ActualHeight - PageContent.RowSpacing;
        }
    }

    private void Tile_Click(object sender, RoutedEventArgs e)
    {
        if (TileOf(sender) is { PageType: { } pageType })
        {
            Frame.Navigate(pageType);
        }
    }

    private void Tile_DragOver(object sender, DragEventArgs e)
    {
        if (TileOf(sender) is { IsAvailable: true } tile)
        {
            DroppedFiles.AcceptIfOffered(e, Strings.Format("DropToOpenIn", tile.Name));
        }
    }

    private async void Tile_Drop(object sender, DragEventArgs e)
    {
        if (TileOf(sender) is not { PageType: { } pageType })
        {
            return;
        }

        var paths = await DroppedFiles.GetPathsAsync(e);
        if (paths.Count > 0)
        {
            Frame.Navigate(pageType, new ToolLaunch(paths));
        }
    }

    private static ToolTile? TileOf(object sender) => (sender as FrameworkElement)?.Tag as ToolTile;
}
