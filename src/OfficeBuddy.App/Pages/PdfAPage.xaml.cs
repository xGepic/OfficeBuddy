using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.Core.Tools;

namespace OfficeBuddy.App.Pages;

public sealed partial class PdfAPage : Page
{
    private readonly PdfASession _session = new();

    public PdfAPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("PdfATitle");
        Shell.Help = Strings.Get("PdfAHelp");
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

    private Task<bool> OpenAsync(IReadOnlyList<string> paths) => Shell.OpenOneAsync(paths, _session.OpenAsync);

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        await Shell.SaveToFileAsync(_session.SuggestedFolder, _session.SuggestedFileName!, _session.SaveAsync);
        Refresh();
    }

    private void Refresh()
    {
        var document = _session.Document;
        EmptyState.Visibility = document is null ? Visibility.Visible : Visibility.Collapsed;
        PagesArea.Visibility = document is null ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = _session.CanSave;
        if (document is not null)
        {
            PageThumbnails.Show(document, _session.Pages);
        }
    }
}
