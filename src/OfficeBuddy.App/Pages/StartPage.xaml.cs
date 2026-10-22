using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.App.Controls;
using OfficeBuddy.App.Services;

namespace OfficeBuddy.App.Pages;

public sealed partial class StartPage : Page
{
    private readonly HashSet<int> _headings = [];

    /// <summary>Files the app was started with, which a Tool opens straight away when its tile is clicked.</summary>
    private ToolLaunch? _launch;

    public StartPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the favorites under their own heading first, then every other Tool under the heading of
    /// its group, each in their usual order. With files the app was started with, only the Tools that
    /// can work on them; if none can, every Tool and why. Groups with no tiles left are left out.
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // Built anew on every visit, so favorites changed in the settings show straight away.
        Func<ToolTile, bool> fits = _ => true;
        if (e.Parameter is ToolLaunch launch)
        {
            if (launch.Kind is { } kind)
            {
                _launch = launch;
                fits = tile => tile.Fits(kind, launch.FilePaths.Count);
                var one = launch.FilePaths.Count == 1;
                HeadingText.Text = one
                    ? Strings.Format("StartHeadingOneFile", Path.GetFileName(launch.FilePaths[0]))
                    : Strings.Format("StartHeadingFiles", launch.FilePaths.Count);
                SubheadingText.Text = Strings.Get(one ? "StartSubheadingOneFile" : "StartSubheadingFiles");
            }
            else
            {
                SubheadingText.Text = Strings.Get("StartFilesUnsupported");
            }
            ShowAllButton.Visibility = Visibility.Visible;
        }

        var favorites = AppSettings.Favorites;
        var items = new List<object>();
        void AddGroup(string headingKey, IEnumerable<ToolTile> tiles)
        {
            var group = tiles.Where(fits).ToList();
            if (group.Count > 0)
            {
                _headings.Add(items.Count);
                items.Add(new TileGroupHeader(Strings.Get(headingKey)));
                items.AddRange(group);
            }
        }

        AddGroup("StartGroupFavorites", ToolTile.All.Where(tile => favorites.Contains(tile.Id)));
        foreach (var group in Enum.GetValues<ToolGroup>())
        {
            AddGroup($"StartGroup{group}", ToolTile.All.Where(tile => tile.Group == group && !favorites.Contains(tile.Id)));
        }
        TileList.ItemsSource = items;
    }

    /// <summary>Leaves the files the app was started with and shows every Tool, as if started plainly.</summary>
    private void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(StartPage));
        Frame.BackStack.RemoveAt(Frame.BackStack.Count - 1);
    }

    /// <summary>Tells the tile grid which items are headings and how much height is left below the header, so all tiles fit without scrolling.</summary>
    private void Layout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (TileList.ItemsPanelRoot is TileGrid grid)
        {
            grid.Headings = _headings;
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
            Frame.Navigate(pageType, _launch);
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
