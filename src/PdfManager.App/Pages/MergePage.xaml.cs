using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using PdfManager.App.Services;
using PdfManager.Core;

namespace PdfManager.App.Pages;

public sealed partial class MergePage : Page
{
    private readonly MergeSession _session = new();
    // Rendered once per Document, however often the list is rebuilt.
    private readonly Dictionary<Document, Task<ImageSource?>> _thumbnails = [];
    private MergeCard? _draggedCard;

    public MergePage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("MergeTitle");
        Shell.Session = _session;
        // Deferred: a move arrives while the ListView is still finishing its own drop handling.
        _session.Changed += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
    }

    /// <summary>What the list shows; rebuilt from the session after every change.</summary>
    public ObservableCollection<MergeCard> Cards { get; } = [];

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ToolLaunch launch)
        {
            await AddDocumentsAsync(launch.FilePaths);
        }
    }

    private void Shell_BackRequested(object? sender, EventArgs e) => Frame.GoBack();

    private async void Shell_FilesOpened(object? sender, IReadOnlyList<string> paths) => await AddDocumentsAsync(paths);

    private async Task AddDocumentsAsync(IReadOnlyList<string> paths)
    {
        IReadOnlyList<LoadFailure> failures;
        using (Shell.ShowBusy(Strings.Get("BusyOpening")))
        {
            failures = await _session.AddAsync(paths);
        }

        Shell.ShowLoadFailures(failures);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is MergeCard card)
        {
            _session.Remove(card.Index);
        }
    }

    private void DocumentList_DragItemsStarting(object sender, DragItemsStartingEventArgs e) =>
        _draggedCard = e.Items.FirstOrDefault() as MergeCard;

    private void DocumentList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
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
        for (var i = 0; i < _session.Documents.Count; i++)
        {
            var card = new MergeCard(_session.Documents[i], i);
            Cards.Add(card);
            _ = ShowThumbnailAsync(card);
        }

        var hasDocuments = Cards.Count > 0;
        EmptyState.Visibility = hasDocuments ? Visibility.Collapsed : Visibility.Visible;
        ListArea.Visibility = hasDocuments ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = _session.CanSave;
        NeedTwoHint.Visibility = _session.CanSave ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task ShowThumbnailAsync(MergeCard card)
    {
        if (!_thumbnails.TryGetValue(card.Document, out var thumbnail))
        {
            thumbnail = PageImages.RenderAsync(card.Document, 0, MergeCard.ThumbnailWidth, MergeCard.ThumbnailHeight);
            _thumbnails[card.Document] = thumbnail;
        }
        card.Thumbnail = await thumbnail;
    }
}

/// <summary>One Document in the Merge list, at its current position.</summary>
public sealed class MergeCard(Document document, int index) : INotifyPropertyChanged
{
    public const double ThumbnailWidth = 48;
    public const double ThumbnailHeight = 64;

    private ImageSource? _thumbnail;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Document Document { get; } = document;

    public int Index { get; } = index;

    /// <summary>The first Page, once it is drawn.</summary>
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

    public string Name => Document.FileName;

    public string PageCountText => Strings.PageCount(Document.PageCount);

    public string RemoveLabel => Strings.Get("MergeRemove");
}
