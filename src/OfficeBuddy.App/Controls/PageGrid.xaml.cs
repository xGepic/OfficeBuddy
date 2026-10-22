using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OfficeBuddy.App.Services;
using OfficeBuddy.Core.Pdf;
using Windows.Foundation;
using Windows.System;

namespace OfficeBuddy.App.Controls;

/// <summary>
/// The Pages of a Document as thumbnails with page numbers, filled in one after another. Supports
/// single, Ctrl and Shift selection (or selecting by toggling, or none), optionally dragging a Page to
/// a new position, a large Preview on double-click that closes by button, Escape or clicking outside
/// it, and for Split clickable gaps between Pages with a coloured band per Part.
/// </summary>
public sealed partial class PageGrid : UserControl
{
    private const double ThumbnailSize = 168;

    // Clearly different colours that read on light and dark backgrounds, one per Part in turn.
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush[] PartBrushes =
    [
        new(Windows.UI.Color.FromArgb(255, 0x00, 0x78, 0xD4)),
        new(Windows.UI.Color.FromArgb(255, 0xCA, 0x50, 0x10)),
        new(Windows.UI.Color.FromArgb(255, 0x49, 0x82, 0x05)),
        new(Windows.UI.Color.FromArgb(255, 0x87, 0x64, 0xB8)),
        new(Windows.UI.Color.FromArgb(255, 0x03, 0x83, 0x87)),
        new(Windows.UI.Color.FromArgb(255, 0xC2, 0x39, 0xB3)),
    ];

    // Every Page shown since the Document was opened, by where it comes from. Kept when a Page
    // leaves the grid, so undoing and redoing an insert doesn't draw its thumbnails again.
    private readonly Dictionary<(Document? InsertedFrom, int SourceIndex), PageThumbnail> _thumbnails = [];
    private Document? _document;
    private CancellationTokenSource? _loading;
    private PageThumbnail? _dragged;
    private int _draggedFrom;
    private int _previewVersion;
    private bool _isSettingSelection;
    private bool _selectByToggling;
    private bool _allowSelection = true;
    // When selecting by toggling, both clicks of a double-click toggle the Page. Changes are passed
    // on only once no second click can follow, so opening the Preview leaves the selection alone.
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _selectionSettling;
    // The selection as last passed on (or set from outside), to restore a Page after a double-click.
    private HashSet<int> _passedOnPositions = [];

    public PageGrid()
    {
        InitializeComponent();
        Unloaded += (_, _) => _loading?.Cancel();
        _selectionSettling = DispatcherQueue.CreateTimer();
        _selectionSettling.Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime());
        _selectionSettling.IsRepeating = false;
        _selectionSettling.Tick += (_, _) => PassOnSelection();
        // Focus can only move into the popup once it is shown.
        PreviewPopup.Opened += (_, _) => DispatcherQueue.TryEnqueue(() => PreviewCloseButton.Focus(FocusState.Programmatic));
        // Escape closes the Preview even if focus stayed on the thumbnails.
        AddHandler(KeyDownEvent, new KeyEventHandler(PreviewRoot_KeyDown), handledEventsToo: true);
    }

    public ObservableCollection<PageThumbnail> Items { get; } = [];

    /// <summary>Positions of the selected Pages, in display order.</summary>
    public IReadOnlyList<int> SelectedPositions =>
        [.. Thumbnails.SelectedItems.OfType<PageThumbnail>().Select(Items.IndexOf).Where(i => i >= 0).Order()];

    /// <summary>Whether Pages can be dragged to a new position. On by default.</summary>
    public bool CanReorder
    {
        get => Thumbnails.CanReorderItems;
        set => (Thumbnails.CanReorderItems, Thumbnails.CanDragItems, Thumbnails.AllowDrop) = (value, value, value);
    }

    /// <summary>
    /// A click turns a Page's selection on or off, without Ctrl or Shift. For Tools where the
    /// selection is the result, so one stray click doesn't lose it. Off by default.
    /// </summary>
    public bool SelectByToggling
    {
        get => _selectByToggling;
        set
        {
            _selectByToggling = value;
            UpdateSelectionMode();
        }
    }

    /// <summary>Whether Pages can be selected at all. On by default.</summary>
    public bool AllowSelection
    {
        get => _allowSelection;
        set
        {
            _allowSelection = value;
            UpdateSelectionMode();
        }
    }

    /// <summary>Raised with the position of the Page after the gap the user clicked (Split).</summary>
    public event EventHandler<int>? GapClicked;

    /// <summary>Raised when the user changed the selection (not when it is set with <see cref="SetSelectedPositions"/>).</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised after the user dragged a single Page to a new position.</summary>
    public event EventHandler<PageMove>? PageMoved;

    /// <summary>Raised when a drag was refused because several Pages were selected.</summary>
    public event EventHandler? SeveralDragged;

    /// <summary>
    /// Shows <paramref name="pages"/> of <paramref name="document"/>, including Pages inserted from
    /// other Documents, keeping thumbnails that are already drawn.
    /// </summary>
    public void Show(Document? document, IReadOnlyList<PageEntry> pages)
    {
        if (PreviewPopup.IsOpen)
        {
            // The Preview shows the Page as it was when opened; don't leave it out of date.
            ClosePreview();
        }

        if (!ReferenceEquals(document, _document))
        {
            _loading?.Cancel();
            _loading = new CancellationTokenSource();
            _document = document;
            _thumbnails.Clear();
            Items.Clear();
        }

        if (document is null)
        {
            return;
        }

        var shown = new List<PageThumbnail>(pages.Count);
        var added = new List<PageThumbnail>();
        foreach (var page in pages)
        {
            if (!_thumbnails.TryGetValue((page.InsertedFrom, page.SourceIndex), out var thumbnail))
            {
                thumbnail = new PageThumbnail(page.InsertedFrom ?? document, page);
                _thumbnails[(page.InsertedFrom, page.SourceIndex)] = thumbnail;
                added.Add(thumbnail);
            }
            shown.Add(thumbnail);
        }

        // Pages no longer there, e.g. after undoing an insert.
        var stillShown = shown.ToHashSet();
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (!stillShown.Contains(Items[i]))
            {
                Items.RemoveAt(i);
            }
        }

        for (var position = 0; position < pages.Count; position++)
        {
            var thumbnail = shown[position];
            thumbnail.Update(pages[position]);
            var current = Items.IndexOf(thumbnail);
            if (current < 0)
            {
                Items.Insert(position, thumbnail);
            }
            else if (current != position)
            {
                Items.Move(current, position);
            }
        }

        if (added.Count > 0)
        {
            _ = LoadThumbnailsAsync(added, _loading!.Token);
        }
    }

    /// <summary>Scrolls the Page at this position into view, e.g. Pages just inserted.</summary>
    public void ScrollIntoView(int position) => Thumbnails.ScrollIntoView(Items[position]);

    /// <summary>
    /// Shows a Split under the thumbnails: a coloured band per Part, and clickable gaps between
    /// Pages with scissors where a Split point is. Positions are the Pages' positions in the grid.
    /// </summary>
    /// <param name="splitPoints">Positions of the Pages that start a new Part because of a Split point, i.e. come after a gap with scissors.</param>
    /// <param name="gapsEnabled">Whether gaps can be clicked; off while each Page is its own Part.</param>
    public void ShowSplit(IReadOnlyList<PageRange> parts, IReadOnlySet<int> splitPoints, bool gapsEnabled)
    {
        for (var number = 1; number <= parts.Count; number++)
        {
            var part = parts[number - 1];
            var brush = PartBrushes[(number - 1) % PartBrushes.Length];
            for (var position = part.First; position <= part.Last && position < Items.Count; position++)
            {
                var hasGapAfter = gapsEnabled && position < Items.Count - 1;
                Items[position].ShowSplit(new SplitMark(
                    number, brush, position == part.First, hasGapAfter, hasGapAfter && splitPoints.Contains(position + 1)));
            }
        }
    }

    /// <summary>Selects exactly the Pages at these positions, e.g. after an undo.</summary>
    public void SetSelectedPositions(IReadOnlyCollection<int> positions)
    {
        if (positions.Order().SequenceEqual(SelectedPositions))
        {
            return;
        }

        _selectionSettling.Stop();
        _passedOnPositions = [.. positions];
        _isSettingSelection = true;
        try
        {
            Thumbnails.SelectedItems.Clear();
            foreach (var position in positions)
            {
                Thumbnails.SelectedItems.Add(Items[position]);
            }
        }
        finally
        {
            _isSettingSelection = false;
        }
    }

    /// <summary>
    /// Passes on a selection change that is still waiting for a possible double-click, e.g. right
    /// before saving.
    /// </summary>
    public void CommitSelection()
    {
        if (_selectionSettling.IsRunning)
        {
            _selectionSettling.Stop();
            PassOnSelection();
        }
    }

    private void PassOnSelection()
    {
        _passedOnPositions = [.. SelectedPositions];
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// When selecting by toggling, the clicks of a double-click toggled the Page; put it back the way
    /// it was, so opening the Preview doesn't change the selection.
    /// </summary>
    private void RestoreSelectionAfterDoubleClick(PageThumbnail thumbnail)
    {
        var wasSelected = _passedOnPositions.Contains(Items.IndexOf(thumbnail));
        _isSettingSelection = true;
        try
        {
            if (wasSelected && !Thumbnails.SelectedItems.Contains(thumbnail))
            {
                Thumbnails.SelectedItems.Add(thumbnail);
            }
            else if (!wasSelected && Thumbnails.SelectedItems.Contains(thumbnail))
            {
                Thumbnails.SelectedItems.Remove(thumbnail);
            }
        }
        finally
        {
            _isSettingSelection = false;
        }

        // Other clicks just before the double-click still count.
        if (_passedOnPositions.SetEquals(SelectedPositions))
        {
            _selectionSettling.Stop();
        }
    }

    private static async Task LoadThumbnailsAsync(IReadOnlyList<PageThumbnail> thumbnails, CancellationToken cancellation)
    {
        foreach (var thumbnail in thumbnails)
        {
            var image = await PageImages.RenderAsync(thumbnail.Source, thumbnail.SourceIndex, ThumbnailSize, ThumbnailSize);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }
            thumbnail.Image = image;
        }
    }

    private void Thumbnails_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (var thumbnail in e.RemovedItems.OfType<PageThumbnail>())
        {
            thumbnail.IsSelected = false;
        }
        foreach (var thumbnail in e.AddedItems.OfType<PageThumbnail>())
        {
            thumbnail.IsSelected = true;
        }

        if (_isSettingSelection)
        {
            return;
        }

        if (_selectByToggling)
        {
            _selectionSettling.Stop();
            _selectionSettling.Start();
        }
        else
        {
            PassOnSelection();
        }
    }

    private void Thumbnails_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count != 1)
        {
            // Pages are reordered one at a time; dragging several at once would scramble the order.
            e.Cancel = true;
            SeveralDragged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _dragged = (PageThumbnail)e.Items[0];
        _draggedFrom = Items.IndexOf(_dragged);
    }

    private void Thumbnails_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (_dragged is not { } thumbnail)
        {
            return;
        }

        _dragged = null;
        var to = Items.IndexOf(thumbnail);
        if (to >= 0 && to != _draggedFrom)
        {
            PageMoved?.Invoke(this, new PageMove(_draggedFrom, to));
        }
    }

    private async void Thumbnails_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not PageThumbnail thumbnail)
        {
            return;
        }

        if (_selectByToggling)
        {
            RestoreSelectionAfterDoubleClick(thumbnail);
        }

        var version = ++_previewVersion;
        var window = XamlRoot.Size;
        // A square box, so the Page fits however it is rotated.
        var side = Math.Max(100, Math.Min(window.Width * 0.9 - 40, window.Height * 0.9 - 110));

        // The popup sits inside this control; shift it so it covers the whole window.
        var origin = TransformToVisual(null).TransformPoint(new Point(0, 0));
        PreviewPopup.HorizontalOffset = -origin.X;
        PreviewPopup.VerticalOffset = -origin.Y;
        PreviewRoot.Width = window.Width;
        PreviewRoot.Height = window.Height;
        PreviewBox.Width = side;
        PreviewBox.Height = side;
        PreviewTitle.Text = Strings.Format("PreviewTitle", thumbnail.PageNumberText);
        PreviewRotation.Angle = thumbnail.Rotation;
        PreviewImage.Source = null;
        PreviewProgress.IsActive = true;
        PreviewPopup.IsOpen = true;

        var image = await PageImages.RenderAsync(thumbnail.Source, thumbnail.SourceIndex, side, side);
        if (version == _previewVersion && PreviewPopup.IsOpen)
        {
            PreviewProgress.IsActive = false;
            PreviewImage.Source = image;
        }
    }

    private void Gap_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PageThumbnail thumbnail })
        {
            GapClicked?.Invoke(this, Items.IndexOf(thumbnail) + 1);
        }
    }

    private void UpdateSelectionMode() =>
        Thumbnails.SelectionMode = !_allowSelection ? ListViewSelectionMode.None
            : _selectByToggling ? ListViewSelectionMode.Multiple
            : ListViewSelectionMode.Extended;

    private void ClosePreview()
    {
        _previewVersion++;
        PreviewPopup.IsOpen = false;
        PreviewImage.Source = null;
        Thumbnails.Focus(FocusState.Programmatic);
    }

    private void PreviewClose_Click(object sender, RoutedEventArgs e) => ClosePreview();

    private void PreviewRoot_Tapped(object sender, TappedRoutedEventArgs e) => ClosePreview();

    // Clicks on the Preview itself must not count as clicking outside it.
    private void PreviewCard_Tapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    private void PreviewRoot_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && PreviewPopup.IsOpen)
        {
            ClosePreview();
            e.Handled = true;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();
}
