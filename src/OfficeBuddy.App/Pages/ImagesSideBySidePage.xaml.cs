using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.App.Services;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

public sealed partial class ImagesSideBySidePage : Page
{
    // The preview is drawn to fit this box; the Image scales it to the space it gets.
    private const double PreviewWidth = 900;
    private const double PreviewHeight = 700;

    private readonly ImagesSideBySideSession _session = new();
    // Rendered once per image, however often the list is rebuilt.
    private readonly Dictionary<ImageFile, Task<ImageSource?>> _thumbnails = [];
    private ImageCard? _draggedCard;
    // Counts previews asked for, so one that finishes late doesn't replace a newer one.
    private int _previewVersion;
    // While the arrangement is shown from the session, its change is not the user's.
    private bool _isRefreshing;

    public ImagesSideBySidePage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("ImagesSideBySideTitle");
        Shell.Opens = OpenableFiles.Images;
        Shell.Session = _session;
        // Deferred: a move arrives while the ListView is still finishing its own drop handling.
        _session.Changed += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
    }

    /// <summary>What the list shows; rebuilt from the session after every change.</summary>
    public ObservableCollection<ImageCard> Cards { get; } = [];

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ToolLaunch launch)
        {
            await AddImagesAsync(launch.FilePaths);
        }
    }

    private void Shell_BackRequested(object? sender, EventArgs e) => Frame.GoBack();

    private async void Shell_FilesOpened(object? sender, IReadOnlyList<string> paths) => await AddImagesAsync(paths);

    private async Task AddImagesAsync(IReadOnlyList<string> paths)
    {
        IReadOnlyList<LoadFailure> failures;
        using (Shell.ShowBusy(Strings.Get("BusyOpeningImages")))
        {
            failures = await _session.AddAsync(paths);
        }

        Shell.ShowLoadFailures(failures);
    }

    private void ArrangementChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isRefreshing && ArrangementChoice.SelectedIndex >= 0)
        {
            _session.Arrange(ArrangementChoice.SelectedIndex == 0 ? ImageArrangement.SideBySide : ImageArrangement.OneBelowTheOther);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ImageCard card })
        {
            _session.Remove(card.Index);
        }
    }

    private void ImageList_DragItemsStarting(object sender, DragItemsStartingEventArgs e) =>
        _draggedCard = e.Items.FirstOrDefault() as ImageCard;

    private void ImageList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
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
        await Shell.SaveToFileAsync(_session.SuggestedFolder, _session.SuggestedFileName!, _session.SaveAsync,
            _session.SavesAsPng ? FileFilter.Png : FileFilter.Jpeg);
        Refresh();
    }

    private void Refresh()
    {
        Cards.Clear();
        for (var i = 0; i < _session.Images.Count; i++)
        {
            var image = _session.Images[i];
            var card = new ImageCard(image, i, Strings.Format("ImagePixelSize", image.Width, image.Height));
            Cards.Add(card);
            _ = ShowThumbnailAsync(card);
        }

        var hasImages = Cards.Count > 0;
        EmptyState.Visibility = hasImages ? Visibility.Collapsed : Visibility.Visible;
        ListArea.Visibility = hasImages ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = _session.CanSave;

        _isRefreshing = true;
        ArrangementChoice.SelectedIndex = _session.Arrangement == ImageArrangement.SideBySide ? 0 : 1;
        _isRefreshing = false;

        ResultText.Text = _session.ResultSize is not { } size
            ? ""
            : _session.CanSave
                ? Strings.Format("ImagesSideBySideResultSize", size.Width, size.Height)
                : Strings.Get("ImagesSideBySideAddMore");
        _ = ShowPreviewAsync();
    }

    private async Task ShowPreviewAsync()
    {
        var version = ++_previewVersion;
        if (_session.Images.Count == 0)
        {
            Preview.Source = null;
            return;
        }

        var preview = await PageImages.RenderAsync(_session, PreviewWidth, PreviewHeight);
        if (version == _previewVersion)
        {
            Preview.Source = preview;
        }
    }

    private async Task ShowThumbnailAsync(ImageCard card)
    {
        if (!_thumbnails.TryGetValue(card.Image, out var thumbnail))
        {
            thumbnail = PageImages.RenderAsync(card.Image, ImageCard.ThumbnailSize, ImageCard.ThumbnailSize);
            _thumbnails[card.Image] = thumbnail;
        }
        card.Thumbnail = await thumbnail;
    }
}
