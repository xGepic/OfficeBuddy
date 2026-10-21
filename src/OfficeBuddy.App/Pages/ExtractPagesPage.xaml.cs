using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

public sealed partial class ExtractPagesPage : Page
{
    private readonly ExtractPagesSession _session = new();

    public ExtractPagesPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("ExtractPagesTitle");
        Shell.Session = _session;
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

    private Task OpenAsync(IReadOnlyList<string> paths) => Shell.OpenOneAsync(paths, _session.OpenAsync);

    // Without reordering, a position in the grid is the Page's index in the Document.
    private void PageThumbnails_SelectionChanged(object? sender, EventArgs e) =>
        _session.SetSelection(PageThumbnails.SelectedPositions);

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        // A click just before saving still counts.
        PageThumbnails.CommitSelection();
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
        PageThumbnails.SetSelectedPositions(_session.SelectedPageIndexes);

        SelectionText.Text = Strings.PagesSelected(_session.SelectedPageIndexes.Count);
        SelectionText.Visibility = hasDocument ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = _session.CanSave;
    }
}
