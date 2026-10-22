using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OfficeBuddy.App.Services;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

/// <summary>
/// Lets the user choose where the save dialog opens, how big their emails may be, and favorite
/// Tools, whose tiles the start screen shows first. Every change is saved at once.
/// </summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        SaveLocationChoice.SelectedIndex = (int)AppSettings.SaveLocation;
        foreach (var megabytes in AppSettings.EmailSizeLimits)
        {
            EmailSizeChoice.Items.Add(Strings.Format(
                megabytes == CompressSession.DefaultEmailSizeLimitMegabytes ? "SettingsEmailSizeDefault" : "SettingsEmailSize", megabytes));
        }
        EmailSizeChoice.SelectedIndex = AppSettings.EmailSizeLimits.ToList().IndexOf(AppSettings.EmailSizeLimitMegabytes);
        ShowChosenFolder();
    }

    public IReadOnlyList<ToolTile> Tiles => ToolTile.All;

    private void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();

    /// <summary>Choosing "another folder" for the first time asks for it straight away; cancelling keeps the old choice.</summary>
    private void SaveLocation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var location = (SaveLocation)SaveLocationChoice.SelectedIndex;
        if (SaveLocationChoice.SelectedIndex < 0 || location == AppSettings.SaveLocation)
        {
            return;
        }

        if (location == SaveLocation.ChosenFolder && AppSettings.SaveFolder is null)
        {
            if (PickFolder() is not { } folder)
            {
                SaveLocationChoice.SelectedIndex = (int)AppSettings.SaveLocation;
                return;
            }
            AppSettings.SetSaveLocation(location, folder);
        }
        else
        {
            AppSettings.SetSaveLocation(location);
        }
        ShowChosenFolder();
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        if (PickFolder() is { } folder)
        {
            AppSettings.SetSaveLocation(SaveLocation.ChosenFolder, folder);
            ShowChosenFolder();
        }
    }

    /// <returns>The folder the user chose, or null if they cancelled or the dialog failed.</returns>
    private static string? PickFolder()
    {
        try
        {
            return FileDialogs.PickFolder(App.MainWindow.Handle, Strings.Get("SettingsChooseFolderDialogTitle"), AppSettings.SaveFolder);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void ShowChosenFolder()
    {
        ChosenFolderPanel.Visibility = AppSettings.SaveLocation == SaveLocation.ChosenFolder ? Visibility.Visible : Visibility.Collapsed;
        ChosenFolderText.Text = AppSettings.SaveFolder ?? "";
    }

    private void EmailSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EmailSizeChoice.SelectedIndex >= 0)
        {
            AppSettings.SetEmailSizeLimitMegabytes(AppSettings.EmailSizeLimits[EmailSizeChoice.SelectedIndex]);
        }
    }

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
