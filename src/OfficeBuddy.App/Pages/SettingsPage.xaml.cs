using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OfficeBuddy.App.Services;

namespace OfficeBuddy.App.Pages;

/// <summary>Lets the user pick favorite Tools, whose tiles the start screen shows first. Every change is saved at once.</summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    public IReadOnlyList<ToolTile> Tiles => ToolTile.All;

    private void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();

    /// <summary>Ticks the boxes of the current favorites; the Checked this raises changes nothing.</summary>
    private void Favorite_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: ToolTile tile } box)
        {
            box.IsChecked = AppSettings.Favorites.Contains(tile.Id);
        }
    }

    private void Favorite_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: ToolTile tile } box)
        {
            AppSettings.SetFavorite(tile.Id, box.IsChecked == true);
        }
    }
}
