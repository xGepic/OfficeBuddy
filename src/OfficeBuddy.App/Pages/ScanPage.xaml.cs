using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.App.Services;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

public sealed partial class ScanPage : Page
{
    private readonly ScanSession _session = new();
    // Rendered once per picture and turn, however often the list is rebuilt.
    private readonly Dictionary<(ImageFile Image, int Rotation), Task<ImageSource?>> _thumbnails = [];
    private IReadOnlyList<ScannerInfo> _scanners = [];
    private IReadOnlyList<ScanSource> _sources = [];
    private ImageCard? _draggedCard;

    public ScanPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("ScanTitle");
        // Öffnen and dropping add pictures that were scanned before, or photos.
        Shell.Opens = OpenableFiles.Images;
        Shell.Session = _session;
        // Deferred: a move arrives while the ListView is still finishing its own drop handling.
        _session.Changed += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
        Refresh();
    }

    /// <summary>What the list shows; rebuilt from the session after every change.</summary>
    public ObservableCollection<ImageCard> Cards { get; } = [];

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ToolLaunch launch)
        {
            await AddPicturesAsync(launch.FilePaths);
        }
        await FindScannersAsync();
    }

    private void Shell_BackRequested(object? sender, EventArgs e) => Frame.GoBack();

    private async void Shell_FilesOpened(object? sender, IReadOnlyList<string> paths) => await AddPicturesAsync(paths);

    private async Task AddPicturesAsync(IReadOnlyList<string> paths)
    {
        IReadOnlyList<LoadFailure> failures;
        using (Shell.ShowBusy(Strings.Get("BusyOpeningImages")))
        {
            failures = await _session.AddAsync(paths);
        }

        Shell.ShowLoadFailures(failures);
    }

    /// <summary>Offers the connected scanners, the first one chosen.</summary>
    private async Task FindScannersAsync()
    {
        _scanners = await Scanners.FindAsync();
        ScannerChoice.Items.Clear();
        foreach (var scanner in _scanners)
        {
            ScannerChoice.Items.Add(scanner.Name);
        }
        ScannerChoice.SelectedIndex = _scanners.Count > 0 ? 0 : -1;
        ScannerChoice.Visibility = _scanners.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        ShowSources();
    }

    private void ScannerChoice_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowSources();

    /// <summary>Offers where the paper can lie on the chosen scanner; automatic first.</summary>
    private void ShowSources()
    {
        var scanner = ScannerChoice.SelectedIndex >= 0 ? _scanners[ScannerChoice.SelectedIndex] : null;
        _sources =
        [
            ScanSource.Automatic,
            .. scanner is { HasFlatbed: true } ? [ScanSource.Flatbed] : Array.Empty<ScanSource>(),
            .. scanner is { HasFeeder: true } ? [ScanSource.Feeder] : Array.Empty<ScanSource>(),
        ];
        SourceChoice.Items.Clear();
        foreach (var source in _sources)
        {
            SourceChoice.Items.Add(Strings.Get($"ScanSource{source switch { ScanSource.Flatbed => "Flatbed", ScanSource.Feeder => "Feeder", _ => "Auto" }}"));
        }
        SourceChoice.SelectedIndex = 0;
        SourceChoice.Visibility = _sources.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_scanners.Count == 0)
        {
            // Maybe switched on since the screen opened.
            await FindScannersAsync();
            if (_scanners.Count == 0)
            {
                Shell.ShowError(Strings.Get("ScanNoScanner"));
                return;
            }
        }

        var scanner = _scanners[Math.Max(0, ScannerChoice.SelectedIndex)];
        var source = _sources[Math.Max(0, SourceChoice.SelectedIndex)];
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "OfficeBuddy", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            IReadOnlyList<LoadFailure> failures;
            using (Shell.ShowBusy(Strings.Get("BusyScanning")))
            {
                var scanned = await Scanners.ScanAsync(scanner, source, folder);
                if (scanned.Count == 0)
                {
                    Shell.ShowError(Strings.Get("ScanFailed"));
                    return;
                }
                failures = await _session.AddAsync(scanned);
            }
            Shell.ShowLoadFailures(failures);
        }
        catch (Exception)
        {
            // No paper, paper jam, switched off, or busy with another program.
            Shell.ShowError(Strings.Get("ScanFailed"));
        }
        finally
        {
            // The session keeps the pictures in memory.
            try { Directory.Delete(folder, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private void Rotate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ImageCard card })
        {
            _session.RotateRight(card.Index);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ImageCard card })
        {
            _session.Remove(card.Index);
        }
    }

    private void PageList_DragItemsStarting(object sender, DragItemsStartingEventArgs e) =>
        _draggedCard = e.Items.FirstOrDefault() as ImageCard;

    private void PageList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (_draggedCard is not { } card)
        {
            return;
        }

        _draggedCard = null;
        var newIndex = Cards.IndexOf(card);
        if (newIndex >= 0 && newIndex != card.Index)
        {
            _session.Move(card.Index, newIndex);
        }
        else
        {
            // No change to the session, but the ListView may have shuffled its items: rebuild after
            // it has finished its own drop handling.
            DispatcherQueue.TryEnqueue(Refresh);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        await Shell.SaveToFileAsync(_session.SuggestedFolder, _session.SuggestedFileName, _session.SaveAsync);
        Refresh();
    }

    private void Refresh()
    {
        Cards.Clear();
        for (var i = 0; i < _session.Pages.Count; i++)
        {
            var page = _session.Pages[i];
            var card = new ImageCard(page.Image, i, Strings.Get(page.IsLandscape ? "ImagePageLandscape" : "ImagePagePortrait"),
                Strings.Format("PageNumber", i + 1));
            Cards.Add(card);
            _ = ShowThumbnailAsync(card, page.Rotation);
        }

        var hasPages = Cards.Count > 0;
        EmptyState.Visibility = hasPages ? Visibility.Collapsed : Visibility.Visible;
        ListArea.Visibility = hasPages ? Visibility.Visible : Visibility.Collapsed;
        ScanText.Text = Strings.Get(hasPages ? "ScanMore" : "ScanStart");
        SaveButton.IsEnabled = _session.CanSave;
    }

    private async Task ShowThumbnailAsync(ImageCard card, int rotation)
    {
        if (!_thumbnails.TryGetValue((card.Image, rotation), out var thumbnail))
        {
            thumbnail = PageImages.RenderAsync(card.Image, ImageCard.ThumbnailSize, ImageCard.ThumbnailSize, rotation);
            _thumbnails[(card.Image, rotation)] = thumbnail;
        }
        card.Thumbnail = await thumbnail;
    }
}
