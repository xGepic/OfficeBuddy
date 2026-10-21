using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PdfManager.App.Services;
using PdfManager.Core;

namespace PdfManager.App.Pages;

public sealed partial class SplitPage : Page
{
    private readonly SplitSession _session = new();

    public SplitPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("SplitTitle");
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

    private Task OpenAsync(IReadOnlyList<string> paths) => Shell.OpenDocumentAsync(paths, _session.OpenAsync);

    // Without reordering, the position of the Page after the gap is its index: where the Split point goes.
    private void PageThumbnails_GapClicked(object? sender, int pageIndex) => _session.ToggleSplitPoint(pageIndex);

    private void EachPageSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        // Refresh sets the switch from the session too; only the user's own toggle is a change.
        if (EachPageSwitch.IsOn != _session.EachPageSeparately)
        {
            _session.SetEachPageSeparately(EachPageSwitch.IsOn);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        await Shell.SaveToFolderAsync(
            _session.SuggestedFolder, _session.SuggestedBaseName!, FileFilter.Pdf, _session.FileNamesFor, _session.SaveAsync);
        Refresh();
    }

    private void Refresh()
    {
        var hasDocument = _session.Document is not null;
        EmptyState.Visibility = hasDocument ? Visibility.Collapsed : Visibility.Visible;
        PagesArea.Visibility = hasDocument ? Visibility.Visible : Visibility.Collapsed;

        PageThumbnails.Show(_session.Document, _session.Pages);
        PageThumbnails.ShowSplit(_session.Parts, _session.SplitPoints, gapsEnabled: !_session.EachPageSeparately);
        EachPageSwitch.IsOn = _session.EachPageSeparately;

        PartsText.Text = _session.CanSave ? Strings.Format("SplitPartCount", _session.Parts.Count)
            : _session.Pages.Count == 1 ? Strings.Get("SplitOnePage")
            : Strings.Get("SplitNeedTwoParts");
        PartsText.Visibility = hasDocument ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = _session.CanSave;
    }
}
