using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OfficeBuddy.App.Controls;
using OfficeBuddy.App.Services;

namespace OfficeBuddy.App.Pages;

public sealed partial class StartPage : Page
{
    private readonly HashSet<int> _headings = [];

    public StartPage()
    {
        // Built anew on every visit, so favorites changed in the settings show straight away.
        var favorites = AppSettings.Favorites;
        var items = new List<object>();
        void AddGroup(string headingKey, IEnumerable<ToolTile> tiles)
        {
            var group = tiles.ToList();
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
        Items = items;
        InitializeComponent();
    }

    /// <summary>
    /// The favorites under their own heading first, then every other Tool under the heading of its
    /// group, each in their usual order. Groups with no tiles left are left out.
    /// </summary>
    public IReadOnlyList<object> Items { get; }

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

    /// <summary>Started from the Downloads folder for the first time: offers to put Office Buddy on the desktop, once.</summary>
    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (AppSettings.DesktopShortcutOffered || !DesktopShortcut.IsRunningFromDownloads)
        {
            return;
        }

        var offer = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Strings.Get("DesktopOfferTitle"),
            Content = Strings.Get("DesktopOfferMessage"),
            PrimaryButtonText = Strings.Get("DesktopOfferConfirm"),
            CloseButtonText = Strings.Get("DesktopOfferDecline"),
            DefaultButton = ContentDialogButton.Primary,
        };
        var accepted = await offer.ShowAsync() == ContentDialogResult.Primary;
        AppSettings.SetDesktopShortcutOffered();
        if (!accepted)
        {
            return;
        }

        string message;
        try
        {
            DesktopShortcut.Create();
            message = Strings.Get("DesktopDone");
        }
        catch (Exception)
        {
            message = Strings.Get("DesktopFailed");
        }
        await new ContentDialog
        {
            XamlRoot = XamlRoot,
            Content = message,
            CloseButtonText = Strings.Get("DesktopOk"),
            DefaultButton = ContentDialogButton.Close,
        }.ShowAsync();
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
