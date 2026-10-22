using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.App.Services;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

public sealed partial class PictureInPicturePage : Page
{
    // Drawn to fit these boxes, sharp enough for a full-screen window; the Images scale them to the space they get.
    private const double MainRenderWidth = 1600;
    private const double MainRenderHeight = 1200;
    private const double InsertedRenderSize = 800;

    private readonly PictureInPictureSession _session = new();
    private ImageFile? _mainShown;
    private ImageFile? _insertedShown;
    // Where the Inserted image was when a drag started, and where it is while being dragged or sized,
    // before the session gets the new place as one undoable change.
    private Placement? _dragStart;
    private Placement? _dragged;

    public PictureInPicturePage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("PictureInPictureTitle");
        Shell.Opens = OpenableFiles.Images;
        Shell.Session = _session;
        var sizeHandleName = Strings.Get("PictureInPictureSizeHandle");
        AutomationProperties.SetName(SizeHandle, sizeHandleName);
        ToolTipService.SetToolTip(SizeHandle, sizeHandleName);
        _session.Changed += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
    }

    public double HandleSize => 22;

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

    /// <summary>
    /// Opens the first file as the Main image. A second one given with it, e.g. when two are
    /// dropped together, is inserted on it.
    /// </summary>
    private async Task OpenAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count > 0 && await Shell.OpenOneAsync([paths[0]], _session.OpenAsync) && paths.Count > 1)
        {
            await InsertAsync(paths[1]);
        }
    }

    private async void Insert_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> paths;
        try
        {
            paths = FileDialogs.OpenFiles(App.MainWindow.Handle, Strings.Get("PictureInPictureInsertDialogTitle"), FileFilter.Images);
        }
        catch (COMException)
        {
            Shell.ShowError(Strings.Get("DialogFailed"));
            return;
        }

        if (paths.Count > 0)
        {
            await InsertAsync(paths[0]);
        }
    }

    private async Task InsertAsync(string path)
    {
        LoadFailure? failure;
        using (Shell.ShowBusy(Strings.Get("BusyOpeningImage")))
        {
            failure = await _session.InsertAsync(path);
        }

        if (failure is not null)
        {
            Shell.ShowLoadFailures([failure]);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e) => _session.Remove();

    private void Manipulation_Starting(object sender, ManipulationStartingRoutedEventArgs e)
    {
        // Measured against the photo, which stays put, not against the image being moved.
        e.Container = Stage;
        _dragStart = _session.Inserted?.Placement;
        _dragged = null;
    }

    private void InsertedFrame_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        var moved = e.Cumulative.Translation;
        _dragged = start with
        {
            Left = Math.Clamp(start.Left + moved.X / Stage.Width, 0, 1 - start.Width),
            Top = Math.Clamp(start.Top + moved.Y / Stage.Height, 0, 1 - _session.HeightFor(start.Width)),
        };
        ShowInserted(_dragged);
    }

    private void SizeHandle_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        // The pointer, from the Inserted image's top left corner, and how far its bottom right corner
        // moves across and down per share of the photo's width.
        var moved = e.Cumulative.Translation;
        var (perWidthX, perWidthY) = (Stage.Width, _session.HeightFor(1) * Stage.Height);
        var pointerX = start.Width * perWidthX + moved.X;
        var pointerY = start.Width * perWidthY + moved.Y;

        // The width whose corner comes closest to the pointer, staying whole on the photo and big
        // enough to grab.
        var width = (pointerX * perWidthX + pointerY * perWidthY) / (perWidthX * perWidthX + perWidthY * perWidthY);
        var maxWidth = Math.Min(1 - start.Left, (1 - start.Top) / _session.HeightFor(1));
        _dragged = start with { Width = Math.Clamp(width, Math.Min(HandleSize / Stage.Width, maxWidth), maxWidth) };
        ShowInserted(_dragged);
    }

    private void Manipulation_Completed(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        var dragged = _dragged;
        (_dragStart, _dragged) = (null, null);
        if (dragged is not null)
        {
            _session.Place(dragged);
        }
        // Where the session put it; also if it stayed where it was, which is no change to the session.
        ShowInserted(_session.Inserted?.Placement);
    }

    private void StageHost_SizeChanged(object sender, SizeChangedEventArgs e) => Layout();

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        await Shell.SaveToFileAsync(_session.SuggestedFolder, _session.SuggestedFileName!, _session.SaveAsync,
            _session.MainImage!.IsPng ? FileFilter.Png : FileFilter.Jpeg);
        Refresh();
    }

    private void Refresh()
    {
        var main = _session.MainImage;
        var inserted = _session.Inserted;
        EmptyState.Visibility = main is null ? Visibility.Visible : Visibility.Collapsed;
        EditArea.Visibility = main is null ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = _session.CanSave;
        RemoveButton.IsEnabled = inserted is not null;
        InsertText.Text = Strings.Get(inserted is null ? "PictureInPictureInsert" : "PictureInPictureInsertOther");
        AutomationProperties.SetName(InsertButton, InsertText.Text);
        HintText.Text = Strings.Get(inserted is null ? "PictureInPictureInsertHint" : "PictureInPicturePlaceHint");
        if (main is null)
        {
            return;
        }

        if (!ReferenceEquals(main, _mainShown))
        {
            _mainShown = main;
            _ = ShowMainAsync(main);
        }
        if (inserted is not null && !ReferenceEquals(inserted.Image, _insertedShown))
        {
            _insertedShown = inserted.Image;
            _ = ShowInsertedAsync(inserted.Image);
        }
        Layout();
    }

    /// <summary>Sizes the photo to fill the space it has, in its own proportions, and puts the Inserted image on it.</summary>
    private void Layout()
    {
        if (_session.MainImage is not { } main || StageHost.ActualWidth <= 0 || StageHost.ActualHeight <= 0)
        {
            return;
        }

        var scale = Math.Min(StageHost.ActualWidth / main.Width, StageHost.ActualHeight / main.Height);
        Stage.Width = main.Width * scale;
        Stage.Height = main.Height * scale;
        ShowInserted(_dragged ?? _session.Inserted?.Placement);
    }

    private void ShowInserted(Placement? placement)
    {
        var visibility = placement is null ? Visibility.Collapsed : Visibility.Visible;
        InsertedFrame.Visibility = visibility;
        SizeHandle.Visibility = visibility;
        if (placement is null || double.IsNaN(Stage.Width))
        {
            return;
        }

        var (left, top) = (placement.Left * Stage.Width, placement.Top * Stage.Height);
        var (width, height) = (placement.Width * Stage.Width, _session.HeightFor(placement.Width) * Stage.Height);
        Canvas.SetLeft(InsertedFrame, left);
        Canvas.SetTop(InsertedFrame, top);
        InsertedFrame.Width = width;
        InsertedFrame.Height = height;
        Canvas.SetLeft(SizeHandle, left + width - HandleSize / 2);
        Canvas.SetTop(SizeHandle, top + height - HandleSize / 2);
    }

    /// <summary>Shows the photo the right way up, unless another one was opened meanwhile.</summary>
    private async Task ShowMainAsync(ImageFile main)
    {
        MainPicture.Source = null;
        var source = await PageImages.RenderAsync(main, MainRenderWidth, MainRenderHeight);
        if (ReferenceEquals(main, _mainShown))
        {
            MainPicture.Source = source;
        }
    }

    /// <summary>Shows the Inserted image the right way up, unless another one was inserted meanwhile.</summary>
    private async Task ShowInsertedAsync(ImageFile image)
    {
        InsertedPicture.Source = null;
        var source = await PageImages.RenderAsync(image, InsertedRenderSize, InsertedRenderSize);
        if (ReferenceEquals(image, _insertedShown))
        {
            InsertedPicture.Source = source;
        }
    }
}
