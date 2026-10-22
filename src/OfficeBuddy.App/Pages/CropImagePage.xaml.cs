using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using OfficeBuddy.App.Services;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

public sealed partial class CropImagePage : Page
{
    // Drawn to fit this box, sharp enough for a full-screen window; the Image scales it to the space it gets.
    private const double RenderWidth = 1600;
    private const double RenderHeight = 1200;
    private const double HandleSize = 20;

    private readonly CropImageSession _session = new();
    private readonly List<Ellipse> _handles;
    private (ImageFile Image, int Rotation)? _shown;
    // What was kept when a drag started, and what is kept while dragging, before the session gets
    // it as one undoable change.
    private CropArea? _dragStart;
    private CropArea? _dragged;

    public CropImagePage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("CropImageTitle");
        Shell.Opens = OpenableFiles.Images;
        Shell.Session = _session;
        _handles = [.. Overlay.Children.OfType<Ellipse>()];
        foreach (var handle in _handles)
        {
            AutomationProperties.SetName(handle, Strings.Get("CropHandle"));
        }
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

    private void RotateLeft_Click(object sender, RoutedEventArgs e) => _session.RotateLeft();

    private void RotateRight_Click(object sender, RoutedEventArgs e) => _session.RotateRight();

    private void KeepWhole_Click(object sender, RoutedEventArgs e) => _session.KeepWhole();

    private void Manipulation_Starting(object sender, ManipulationStartingRoutedEventArgs e)
    {
        // Measured against the photo, which stays put, not against the grip being moved.
        e.Container = Stage;
        _dragStart = _session.Area;
        _dragged = null;
    }

    private void CropFrame_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        var moved = e.Cumulative.Translation;
        _dragged = start with
        {
            Left = Math.Clamp(start.Left + moved.X / Stage.Width, 0, 1 - start.Width),
            Top = Math.Clamp(start.Top + moved.Y / Stage.Height, 0, 1 - start.Height),
        };
        ShowArea(_dragged);
    }

    /// <summary>Moves the edges the grip stands for, keeping them on the photo and far enough apart to grab.</summary>
    private void Handle_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (_dragStart is not { } start || sender is not FrameworkElement { Tag: string edges })
        {
            return;
        }

        var moved = e.Cumulative.Translation;
        var (dx, dy) = (moved.X / Stage.Width, moved.Y / Stage.Height);
        var (minWidth, minHeight) = (Math.Min(1, 2 * HandleSize / Stage.Width), Math.Min(1, 2 * HandleSize / Stage.Height));
        var (left, top, right, bottom) = (start.Left, start.Top, start.Right, start.Bottom);
        if (edges.Contains('L'))
        {
            left = Math.Clamp(left + dx, 0, right - minWidth);
        }
        if (edges.Contains('R'))
        {
            right = Math.Clamp(right + dx, left + minWidth, 1);
        }
        if (edges.Contains('T'))
        {
            top = Math.Clamp(top + dy, 0, bottom - minHeight);
        }
        if (edges.Contains('B'))
        {
            bottom = Math.Clamp(bottom + dy, top + minHeight, 1);
        }
        _dragged = new CropArea(left, top, right - left, bottom - top);
        ShowArea(_dragged);
    }

    private void Manipulation_Completed(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        var dragged = _dragged;
        (_dragStart, _dragged) = (null, null);
        if (dragged is not null)
        {
            _session.SetArea(dragged);
        }
        // What the session kept; also if it stayed the same, which is no change to the session.
        ShowArea(_session.Area);
    }

    private void StageHost_SizeChanged(object sender, SizeChangedEventArgs e) => Layout();

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        await Shell.SaveToFileAsync(_session.SuggestedFolder, _session.SuggestedFileName!, _session.SaveAsync,
            _session.Image!.IsPng ? FileFilter.Png : FileFilter.Jpeg);
        Refresh();
    }

    private void Refresh()
    {
        var image = _session.Image;
        EmptyState.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
        EditArea.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = _session.CanSave;
        if (image is null)
        {
            return;
        }

        KeepWholeButton.IsEnabled = _session.Area != CropArea.Whole;
        var size = _session.ResultSize!;
        NewSizeText.Text = Strings.Format("CropNewSize", size.Width, size.Height);
        if (_shown != (image, _session.Rotation))
        {
            _shown = (image, _session.Rotation);
            _ = ShowPictureAsync(image, _session.Rotation);
        }
        Layout();
    }

    /// <summary>Sizes the photo, as it is turned, to fill the space it has in its own proportions, and puts the frame on it.</summary>
    private void Layout()
    {
        if (_session.TurnedSize is not { } turned || StageHost.ActualWidth <= 0 || StageHost.ActualHeight <= 0)
        {
            return;
        }

        var scale = Math.Min(StageHost.ActualWidth / turned.Width, StageHost.ActualHeight / turned.Height);
        Stage.Width = turned.Width * scale;
        Stage.Height = turned.Height * scale;
        ShowArea(_dragged ?? _session.Area);
    }

    /// <summary>Puts the frame and its grips around <paramref name="area"/> and shades everything outside it.</summary>
    private void ShowArea(CropArea area)
    {
        var (width, height) = (Stage.Width, Stage.Height);
        if (double.IsNaN(width))
        {
            return;
        }

        var (left, top, right, bottom) = (area.Left * width, area.Top * height, area.Right * width, area.Bottom * height);
        Place(ShadeTop, 0, 0, width, top);
        Place(ShadeBottom, 0, bottom, width, height - bottom);
        Place(ShadeLeft, 0, top, left, bottom - top);
        Place(ShadeRight, right, top, width - right, bottom - top);
        Place(CropFrame, left, top, right - left, bottom - top);

        foreach (var handle in _handles)
        {
            var edges = (string)handle.Tag;
            var x = edges.Contains('L') ? left : edges.Contains('R') ? right : (left + right) / 2;
            var y = edges.Contains('T') ? top : edges.Contains('B') ? bottom : (top + bottom) / 2;
            Canvas.SetLeft(handle, x - HandleSize / 2);
            Canvas.SetTop(handle, y - HandleSize / 2);
        }
    }

    private static void Place(FrameworkElement element, double left, double top, double width, double height)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
    }

    /// <summary>Shows the photo as it is turned, unless it was turned again or another one was opened meanwhile.</summary>
    private async Task ShowPictureAsync(ImageFile image, int rotation)
    {
        // Not the old picture squeezed into the new shape while the new one is drawn.
        Picture.Source = null;
        var source = await PageImages.RenderAsync(image, RenderWidth, RenderHeight, rotation);
        if (_shown == (image, rotation))
        {
            Picture.Source = source;
        }
    }
}
