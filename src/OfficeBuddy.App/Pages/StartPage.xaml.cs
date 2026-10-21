using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OfficeBuddy.App.Controls;
using OfficeBuddy.App.Services;

namespace OfficeBuddy.App.Pages;

public sealed partial class StartPage : Page
{
    private readonly int _favoriteCount;

    public StartPage()
    {
        // Built anew on every visit, so favorites changed in the settings show straight away.
        var favorites = AppSettings.Favorites;
        Tiles = [.. ToolTile.All.Where(tile => favorites.Contains(tile.Id)), .. ToolTile.All.Where(tile => !favorites.Contains(tile.Id))];
        _favoriteCount = Tiles.Count(tile => favorites.Contains(tile.Id));
        InitializeComponent();
    }

    /// <summary>The favorites first, then the other Tools, each in their usual order.</summary>
    public IReadOnlyList<ToolTile> Tiles { get; }

    /// <summary>Tells the tile grid which tiles are favorites and how much height is left below the header, so all tiles fit without scrolling.</summary>
    private void Layout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (TileList.ItemsPanelRoot is TileGrid grid)
        {
            grid.FavoriteCount = _favoriteCount;
            var padding = PageContent.Padding;
            grid.FitHeight = Scroller.ViewportHeight - padding.Top - padding.Bottom
                - Header.ActualHeight - PageContent.RowSpacing;
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(SettingsPage));

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
