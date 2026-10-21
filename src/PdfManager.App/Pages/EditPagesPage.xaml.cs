using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PdfManager.App.Controls;
using PdfManager.Core;

namespace PdfManager.App.Pages;

public sealed partial class EditPagesPage : Page
{
    private readonly EditPagesSession _session = new();

    public EditPagesPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("EditPagesTitle");
        Shell.Session = _session;
        // Deferred: a move arrives while the GridView is still finishing its own drop handling.
        _session.Changed += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ToolLaunch launch)
        {
            await OpenAsync(launch.FilePaths);
        }
    }

    private void Shell_BackRequested(object? sender, EventArgs e) => Frame.GoBack();

    private async void Shell_FilesOpened(object? sender, IReadOnlyList<string> paths) => await OpenAsync(paths);

    private async Task OpenAsync(IReadOnlyList<string> paths)
    {
        var progress = new Progress<BlankPageProgress>(done =>
            Shell.UpdateBusyMessage(Strings.Format("BusyFindingBlankPages", done.Checked, done.Total)));
        if (await Shell.OpenOneAsync(paths, path => _session.OpenAsync(path, progress)))
        {
            ShowBlankPagesBanner();
        }
    }

    /// <summary>Says which Pages the app found blank and marked by itself, if any.</summary>
    private void ShowBlankPagesBanner()
    {
        var found = _session.DetectedBlankPageNumbers;
        BlankPagesBanner.Message = found.Count switch
        {
            0 => "",
            1 => Strings.Format("BlankPagesFoundOne", found[0]),
            _ => Strings.Format("BlankPagesFoundMany", found.Count, string.Join(", ", found)),
        };
        BlankPagesBanner.IsOpen = found.Count > 0;
    }

    private void RotateLeft_Click(object sender, RoutedEventArgs e) => _session.RotateLeft(PageThumbnails.SelectedPositions);

    private void RotateRight_Click(object sender, RoutedEventArgs e) => _session.RotateRight(PageThumbnails.SelectedPositions);

    /// <summary>Marks the selected Pages for removal, or keeps them again if all of them are marked already.</summary>
    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var selected = PageThumbnails.SelectedPositions;
        if (_session.AreAllMarkedForRemoval(selected))
        {
            _session.Unmark(selected);
        }
        else
        {
            _session.MarkForRemoval(selected);
        }
    }

    private void PageThumbnails_PageMoved(object? sender, PageMove move) => _session.Move(move.From, move.To);

    private void PageThumbnails_SelectionChanged(object? sender, EventArgs e) => UpdatePageActions();

    private void PageThumbnails_SeveralDragged(object? sender, EventArgs e) => Shell.ShowInfo(Strings.Get("EditPagesDragOne"));

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        await Shell.SaveToFileAsync(_session.SuggestedFolder, _session.SuggestedFileName!, _session.SaveAsync);
        Refresh();
    }

    private void Refresh()
    {
        var hasDocument = _session.Document is not null;
        EmptyState.Visibility = hasDocument ? Visibility.Collapsed : Visibility.Visible;
        PagesArea.Visibility = hasDocument ? Visibility.Visible : Visibility.Collapsed;
        PageThumbnails.Show(_session.Document, _session.Pages);
        AllRemovedHint.Visibility = _session.EveryPageMarkedForRemoval ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = _session.CanSave;
        UpdatePageActions();
    }

    /// <summary>The page actions work on the selection; Entfernen turns into Nicht entfernen for marked Pages.</summary>
    private void UpdatePageActions()
    {
        var selected = PageThumbnails.SelectedPositions;
        var hasSelection = selected.Count > 0;
        RotateLeftButton.IsEnabled = hasSelection;
        RotateRightButton.IsEnabled = hasSelection;
        RemoveButton.IsEnabled = hasSelection;

        var keep = _session.AreAllMarkedForRemoval(selected);
        RemoveText.Text = Strings.Get(keep ? "EditPagesKeep" : "EditPagesRemove");
        RemoveIcon.Glyph = keep ? "" : "";
        AutomationProperties.SetName(RemoveButton, RemoveText.Text);
    }
}
