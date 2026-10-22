using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.Core;
using Windows.ApplicationModel.DataTransfer;

namespace OfficeBuddy.App.Pages;

public sealed partial class RecognizeTextPage : Page
{
    private readonly RecognizeTextSession _session = new();
    private readonly bool _isAvailable = RecognizeTextSession.IsAvailable;

    public RecognizeTextPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("RecognizeTextTitle");
        Shell.Session = _session;
        UnavailableBar.Message = Strings.Get("RecognizeTextUnavailable");
        UnavailableBar.IsOpen = !_isAvailable;
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

    /// <summary>Opens the scan and reads its text straight away: that is what the user came for.</summary>
    private async Task OpenAsync(IReadOnlyList<string> paths)
    {
        if (await Shell.OpenOneAsync(paths, _session.OpenAsync) && _isAvailable)
        {
            await RecognizeAsync();
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e) => await RecognizeAsync();

    private async Task RecognizeAsync()
    {
        var progress = new Progress<RecognizeProgress>(at =>
            Shell.UpdateBusyMessage(Strings.Format("BusyRecognizing", Math.Min(at.Done + 1, at.Total), at.Total)));
        try
        {
            using (Shell.ShowBusy(Strings.Format("BusyRecognizing", 1, _session.Document!.PageCount)))
            {
                await _session.RecognizeAsync(progress);
            }
        }
        catch (Exception)
        {
            // Windows' text recognition failed, e.g. its language was removed meanwhile.
            Shell.ShowError(Strings.Get("RecognizeTextUnavailable"));
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(_session.Text);
        Clipboard.SetContent(package);
        Shell.ShowSuccess(Strings.Get("RecognizeTextCopied"));
    }

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
        DocumentArea.Visibility = document is null ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = _session.CanSave;
        if (document is null)
        {
            return;
        }

        NameText.Text = document.FileName;
        PagesText.Text = Strings.PageCount(document.PageCount);
        var recognized = _session.Pages is not null;
        StartButton.Visibility = !recognized && _isAvailable ? Visibility.Visible : Visibility.Collapsed;
        ResultBar.IsOpen = recognized || !_isAvailable;
        ResultBar.Severity = !_isAvailable ? InfoBarSeverity.Warning
            : _session.WordCount > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
        ResultBar.Message = !_isAvailable ? Strings.Get("RecognizeTextUnavailable")
            : _session.WordCount > 0 ? Strings.Format("RecognizeTextFound", Strings.PageCount(document.PageCount), _session.WordCount)
            : Strings.Get("RecognizeTextNothing");
        TextArea.Visibility = _session.WordCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        RecognizedText.Text = _session.Text;
    }
}
