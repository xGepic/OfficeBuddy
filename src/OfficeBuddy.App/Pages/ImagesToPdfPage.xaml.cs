using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.App.Services;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

public sealed partial class ImagesToPdfPage : Page
{
    private readonly ImagesToPdfSession _session = new();
    // Rendered once per image, however often the list is rebuilt.
    private readonly Dictionary<ImageFile, Task<ImageSource?>> _thumbnails = [];
    private ImageCard? _draggedCard;

    public ImagesToPdfPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("ImagesToPdfTitle");
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
        await Shell.SaveToFileAsync(_session.SuggestedFolder, _session.SuggestedFileName!, _session.SaveAsync);
        Refresh();
    }

    private void Refresh()
    {
        Cards.Clear();
        for (var i = 0; i < _session.Images.Count; i++)
        {
            var card = new ImageCard(_session.Images[i], i);
            Cards.Add(card);
            _ = ShowThumbnailAsync(card);
        }

        var hasImages = Cards.Count > 0;
        EmptyState.Visibility = hasImages ? Visibility.Collapsed : Visibility.Visible;
        ListArea.Visibility = hasImages ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = _session.CanSave;
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

/// <summary>One image in the Images to PDF list, at its current position.</summary>
public sealed class ImageCard(ImageFile image, int index) : INotifyPropertyChanged
{
    public const double ThumbnailSize = 64;

    private ImageSource? _thumbnail;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ImageFile Image { get; } = image;

    public int Index { get; } = index;

    /// <summary>The image the right way up, once it is drawn.</summary>
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            _thumbnail = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlaceholderVisibility)));
        }
    }

    public Visibility PlaceholderVisibility => _thumbnail is null ? Visibility.Visible : Visibility.Collapsed;

    public string PositionText => $"{Index + 1}.";

    public string Name => Image.FileName;

    /// <summary>Which way round its A4 Page will be.</summary>
    public string PageText => Strings.Get(Image.IsLandscape ? "ImagePageLandscape" : "ImagePagePortrait");

    public string RemoveLabel => Strings.Get("RemoveFromList");
}
