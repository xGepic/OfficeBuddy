using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Controls;

/// <summary>
/// A picture, such as a Page or a photo, filling the space it has in its own proportions, with
/// another picture placed on it, such as a signature or an Inserted image, that can be dragged
/// into place and sized by its corner handle. While dragging it stays whole on the picture;
/// when the drag ends, <see cref="Placed"/> says where it was put.
/// </summary>
public sealed partial class PlacementStage : UserControl
{
    public static readonly DependencyProperty PictureBackgroundProperty =
        DependencyProperty.Register(nameof(PictureBackground), typeof(Brush), typeof(PlacementStage), new PropertyMetadata(null));

    // The picture's proportions, where the placed picture is as far as the Tool knows, and how
    // many shares of the picture's height it takes per share of the picture's width.
    private double _pictureWidth;
    private double _pictureHeight;
    private Placement? _placement;
    private double _heightPerWidth;
    // Where the placed picture was when a drag started, and where it is while being dragged or
    // sized, before the Tool gets the new place as one undoable change.
    private Placement? _dragStart;
    private Placement? _dragged;

    public PlacementStage()
    {
        InitializeComponent();
        var sizeHandleName = Strings.Get("PlacementSizeHandle");
        AutomationProperties.SetName(SizeHandle, sizeHandleName);
        ToolTipService.SetToolTip(SizeHandle, sizeHandleName);
    }

    /// <summary>Raised when a drag or sizing ends with the place the user chose; the Tool decides where it really goes.</summary>
    public event EventHandler<Placement>? Placed;

    public double HandleSize => 22;

    /// <summary>Shown behind the picture, e.g. white paper while a Page is still being drawn.</summary>
    public Brush? PictureBackground
    {
        get => (Brush?)GetValue(PictureBackgroundProperty);
        set => SetValue(PictureBackgroundProperty, value);
    }

    public ImageSource? Picture
    {
        get => BackgroundImage.Source;
        set => BackgroundImage.Source = value;
    }

    public ImageSource? PlacedPicture
    {
        get => PlacedImage.Source;
        set => PlacedImage.Source = value;
    }

    /// <summary>
    /// Shows a picture of these proportions (in any unit) with the placed picture at
    /// <paramref name="placement"/>, or none, taking <paramref name="heightPerWidth"/> shares of
    /// the picture's height per share of its width. A drag going on carries on.
    /// </summary>
    public void Show(double pictureWidth, double pictureHeight, Placement? placement, double heightPerWidth)
    {
        (_pictureWidth, _pictureHeight, _placement, _heightPerWidth) = (pictureWidth, pictureHeight, placement, heightPerWidth);
        Layout();
    }

    private void StageHost_SizeChanged(object sender, SizeChangedEventArgs e) => Layout();

    private void Manipulation_Starting(object sender, ManipulationStartingRoutedEventArgs e)
    {
        // Measured against the picture, which stays put, not against the one being moved.
        e.Container = Stage;
        _dragStart = _placement;
        _dragged = null;
    }

    private void PlacedFrame_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        var moved = e.Cumulative.Translation;
        _dragged = start with
        {
            Left = Math.Clamp(start.Left + moved.X / Stage.Width, 0, 1 - start.Width),
            Top = Math.Clamp(start.Top + moved.Y / Stage.Height, 0, 1 - start.Width * _heightPerWidth),
        };
        ShowPlaced(_dragged);
    }

    private void SizeHandle_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        // The pointer, from the placed picture's top left corner, and how far its bottom right
        // corner moves across and down per share of the picture's width.
        var moved = e.Cumulative.Translation;
        var (perWidthX, perWidthY) = (Stage.Width, _heightPerWidth * Stage.Height);
        var pointerX = start.Width * perWidthX + moved.X;
        var pointerY = start.Width * perWidthY + moved.Y;

        // The width whose corner comes closest to the pointer, staying whole on the picture and big
        // enough to grab.
        var width = (pointerX * perWidthX + pointerY * perWidthY) / (perWidthX * perWidthX + perWidthY * perWidthY);
        var maxWidth = Math.Min(1 - start.Left, (1 - start.Top) / _heightPerWidth);
        _dragged = start with { Width = Math.Clamp(width, Math.Min(HandleSize / Stage.Width, maxWidth), maxWidth) };
        ShowPlaced(_dragged);
    }

    private void Manipulation_Completed(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        var dragged = _dragged;
        (_dragStart, _dragged) = (null, null);
        if (dragged is not null)
        {
            // The Tool shows where it really put it straight away.
            Placed?.Invoke(this, dragged);
        }
        // Also if it stayed where it was, which is no change for the Tool.
        ShowPlaced(_placement);
    }

    /// <summary>Sizes the picture to fill the space it has, in its own proportions, and puts the placed picture on it.</summary>
    private void Layout()
    {
        if (_pictureWidth <= 0 || _pictureHeight <= 0 || StageHost.ActualWidth <= 0 || StageHost.ActualHeight <= 0)
        {
            return;
        }

        var scale = Math.Min(StageHost.ActualWidth / _pictureWidth, StageHost.ActualHeight / _pictureHeight);
        Stage.Width = _pictureWidth * scale;
        Stage.Height = _pictureHeight * scale;
        ShowPlaced(_dragged ?? _placement);
    }

    private void ShowPlaced(Placement? placement)
    {
        var visibility = placement is null ? Visibility.Collapsed : Visibility.Visible;
        PlacedFrame.Visibility = visibility;
        SizeHandle.Visibility = visibility;
        if (placement is null || double.IsNaN(Stage.Width))
        {
            return;
        }

        var (left, top) = (placement.Left * Stage.Width, placement.Top * Stage.Height);
        var (width, height) = (placement.Width * Stage.Width, placement.Width * _heightPerWidth * Stage.Height);
        Canvas.SetLeft(PlacedFrame, left);
        Canvas.SetTop(PlacedFrame, top);
        PlacedFrame.Width = width;
        PlacedFrame.Height = height;
        Canvas.SetLeft(SizeHandle, left + width - HandleSize / 2);
        Canvas.SetTop(SizeHandle, top + height - HandleSize / 2);
    }
}
