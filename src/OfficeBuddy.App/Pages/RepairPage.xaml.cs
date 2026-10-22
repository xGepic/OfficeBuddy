using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

public sealed partial class RepairPage : Page
{
    private readonly RepairSession _session = new();

    public RepairPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("RepairTitle");
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

    /// <summary>Opens and rebuilds the first file; a damaged one that can't be rescued gets its own message.</summary>
    private async Task OpenAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0 || !await Shell.ConfirmDiscardAsync())
        {
            return;
        }

        LoadFailure? failure;
        using (Shell.ShowBusy(Strings.Get("BusyRepairing")))
        {
            failure = await _session.OpenAsync(paths[0]);
        }

        if (failure is { Reason: LoadFailureReason.Damaged })
        {
            Shell.ShowError(Strings.Format("RepairFailed", failure.FileName));
        }
        else if (failure is not null)
        {
            Shell.ShowLoadFailures([failure]);
        }
        else if (paths.Count > 1)
        {
            Shell.ShowInfo(Strings.Format("OneDocumentOnly", Path.GetFileName(paths[0])));
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        await Shell.SaveToFileAsync(_session.SuggestedFolder, _session.SuggestedFileName!, _session.SaveAsync);
        Refresh();
    }

    private void Refresh()
    {
        var result = _session.Result;
        EmptyState.Visibility = result is null ? Visibility.Visible : Visibility.Collapsed;
        PagesArea.Visibility = result is null ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = _session.CanSave;
        if (result is null)
        {
            return;
        }

        ResultBar.Severity = result.WasDamaged ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
        ResultBar.Message = result.WasDamaged
            ? Strings.Format("RepairRepaired", Strings.PageCount(result.Document.PageCount))
            : Strings.Get("RepairNotNeeded");
        PageThumbnails.Show(result.Document, [.. Enumerable.Range(0, result.Document.PageCount).Select(index => new PageEntry(index))]);
    }
}
