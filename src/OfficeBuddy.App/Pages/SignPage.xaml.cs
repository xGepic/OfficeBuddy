using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.App.Services;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

public sealed partial class SignPage : Page
{
    // Drawn to fit these boxes, sharp enough for a full-screen window; the Images scale them to the space they get.
    private const double PageRenderSize = 1400;
    private const double SignatureRenderWidth = 700;

    private readonly SignSession _session = new();
    private int _pageIndex;
    private (Document Document, int PageIndex)? _pageShown;
    private ImageFile? _signatureShown;
    // Where the signature was when a drag started, and where it is while being dragged or sized,
    // before the session gets the new place as one undoable change.
    private Placement? _dragStart;
    private Placement? _dragged;

    public SignPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("SignTitle");
        Shell.Session = _session;
        var sizeHandleName = Strings.Get("SignSizeHandle");
        AutomationProperties.SetName(SizeHandle, sizeHandleName);
        ToolTipService.SetToolTip(SizeHandle, sizeHandleName);
        foreach (var (button, key) in new[] { (PreviousButton, "SignPreviousPage"), (NextButton, "SignNextPage") })
        {
            AutomationProperties.SetName(button, Strings.Get(key));
            ToolTipService.SetToolTip(button, Strings.Get(key));
        }
        _session.Changed += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
    }

    public double HandleSize => 22;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // The signature chosen last time is ready, so signing the next letter is two clicks.
        if (AppSettings.SignaturePath is { } remembered && File.Exists(remembered))
        {
            await _session.OfferSignatureAsync(remembered);
        }
        if (e.Parameter is ToolLaunch launch)
        {
            await OpenAsync(launch.FilePaths);
        }
    }

    private void Shell_BackRequested(object? sender, EventArgs e) => Frame.GoBack();

    private async void Shell_FilesOpened(object? sender, IReadOnlyList<string> paths) => await OpenAsync(paths);

    private async Task OpenAsync(IReadOnlyList<string> paths)
    {
        if (await Shell.OpenOneAsync(paths, _session.OpenAsync))
        {
            _pageIndex = 0;
            Refresh();
        }
    }

    private async void ChooseSignature_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> paths;
        try
        {
            paths = FileDialogs.OpenFiles(App.MainWindow.Handle, Strings.Get("SignSignatureDialogTitle"), FileFilter.Images);
        }
        catch (COMException)
        {
            Shell.ShowError(Strings.Get("DialogFailed"));
            return;
        }

        if (paths.Count == 0)
        {
            return;
        }

        LoadFailure? failure;
        using (Shell.ShowBusy(Strings.Get("BusyOpeningImage")))
        {
            failure = await _session.ChooseSignatureAsync(paths[0]);
        }

        if (failure is null)
        {
            AppSettings.SetSignaturePath(paths[0]);
        }
        else
        {
            Shell.ShowLoadFailures([failure]);
        }
    }

    private void Previous_Click(object sender, RoutedEventArgs e) => ShowPage(_pageIndex - 1);

    private void Next_Click(object sender, RoutedEventArgs e) => ShowPage(_pageIndex + 1);

    private void SignHere_Click(object sender, RoutedEventArgs e) => _session.Sign(_pageIndex);

    private void RemoveHere_Click(object sender, RoutedEventArgs e) => _session.Unsign(_pageIndex);

    private void ShowPage(int pageIndex)
    {
        _pageIndex = Math.Clamp(pageIndex, 0, _session.Document!.PageCount - 1);
        Refresh();
    }

    private Placement? PlacementHere => _session.Placements.TryGetValue(_pageIndex, out var placement) ? placement : null;

    private void Manipulation_Starting(object sender, ManipulationStartingRoutedEventArgs e)
    {
        // Measured against the Page, which stays put, not against the signature being moved.
        e.Container = Stage;
        _dragStart = PlacementHere;
        _dragged = null;
    }

    private void SignatureFrame_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        var moved = e.Cumulative.Translation;
        _dragged = start with
        {
            Left = Math.Clamp(start.Left + moved.X / Stage.Width, 0, 1 - start.Width),
            Top = Math.Clamp(start.Top + moved.Y / Stage.Height, 0, 1 - _session.HeightFor(_pageIndex, start.Width)),
        };
        ShowSignature(_dragged);
    }

    private void SizeHandle_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        // The pointer, from the signature's top left corner, and how far its bottom right corner
        // moves across and down per share of the Page's width.
        var moved = e.Cumulative.Translation;
        var (perWidthX, perWidthY) = (Stage.Width, _session.HeightFor(_pageIndex, 1) * Stage.Height);
        var pointerX = start.Width * perWidthX + moved.X;
        var pointerY = start.Width * perWidthY + moved.Y;

        // The width whose corner comes closest to the pointer, staying whole on the Page and big
        // enough to grab.
        var width = (pointerX * perWidthX + pointerY * perWidthY) / (perWidthX * perWidthX + perWidthY * perWidthY);
        var maxWidth = Math.Min(1 - start.Left, (1 - start.Top) / _session.HeightFor(_pageIndex, 1));
        _dragged = start with { Width = Math.Clamp(width, Math.Min(HandleSize / Stage.Width, maxWidth), maxWidth) };
        ShowSignature(_dragged);
    }

    private void Manipulation_Completed(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        var dragged = _dragged;
        (_dragStart, _dragged) = (null, null);
        if (dragged is not null)
        {
            _session.Place(_pageIndex, dragged);
        }
        // Where the session put it; also if it stayed where it was, which is no change to the session.
        ShowSignature(PlacementHere);
    }

    private void StageHost_SizeChanged(object sender, SizeChangedEventArgs e) => Layout();

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        await Shell.SaveToFileAsync(_session.SuggestedFolder, _session.SuggestedFileName!, _session.SaveAsync);
        Refresh();
    }

    private void Refresh()
    {
        var document = _session.Document;
        var signature = _session.Signature;
        EmptyState.Visibility = document is null ? Visibility.Visible : Visibility.Collapsed;
        EditArea.Visibility = document is null ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = _session.CanSave;
        ChooseSignatureText.Text = Strings.Get(signature is null ? "SignChooseSignature" : "SignChooseOtherSignature");
        AutomationProperties.SetName(ChooseSignatureButton, ChooseSignatureText.Text);
        if (!ReferenceEquals(signature, _signatureShown))
        {
            _signatureShown = signature;
            _ = ShowSignatureImageAsync(signature);
        }
        if (document is null)
        {
            return;
        }

        _pageIndex = Math.Min(_pageIndex, document.PageCount - 1);
        HintText.Text = Strings.Get(signature is null ? "SignNoSignatureHint" : "SignPlaceHint");
        var signedPages = _session.Placements.Keys.Order().Select(index => index + 1).ToList();
        SignedOnText.Text = signedPages.Count == 0
            ? Strings.Get("SignNotSignedYet")
            : Strings.Format("SignSignedOn", string.Join(", ", signedPages));

        PageText.Text = Strings.Format("SignPageOf", _pageIndex + 1, document.PageCount);
        PreviousButton.IsEnabled = _pageIndex > 0;
        NextButton.IsEnabled = _pageIndex < document.PageCount - 1;
        var signedHere = PlacementHere is not null;
        SignHereButton.IsEnabled = signature is not null && !signedHere;
        RemoveHereButton.Visibility = signedHere ? Visibility.Visible : Visibility.Collapsed;

        if (_pageShown != (document, _pageIndex))
        {
            _pageShown = (document, _pageIndex);
            _ = ShowPageAsync(document, _pageIndex);
        }
        Layout();
    }

    /// <summary>Sizes the Page, as it is shown, to fill the space it has in its own proportions, and puts the signature on it.</summary>
    private void Layout()
    {
        if (_session.Document is not { } document || StageHost.ActualWidth <= 0 || StageHost.ActualHeight <= 0)
        {
            return;
        }

        var size = document.ShownPageSize(_pageIndex);
        var scale = Math.Min(StageHost.ActualWidth / size.Width, StageHost.ActualHeight / size.Height);
        Stage.Width = size.Width * scale;
        Stage.Height = size.Height * scale;
        ShowSignature(_dragged ?? PlacementHere);
    }

    private void ShowSignature(Placement? placement)
    {
        var visibility = placement is null ? Visibility.Collapsed : Visibility.Visible;
        SignatureFrame.Visibility = visibility;
        SizeHandle.Visibility = visibility;
        if (placement is null || double.IsNaN(Stage.Width))
        {
            return;
        }

        var (left, top) = (placement.Left * Stage.Width, placement.Top * Stage.Height);
        var (width, height) = (placement.Width * Stage.Width, _session.HeightFor(_pageIndex, placement.Width) * Stage.Height);
        Canvas.SetLeft(SignatureFrame, left);
        Canvas.SetTop(SignatureFrame, top);
        SignatureFrame.Width = width;
        SignatureFrame.Height = height;
        Canvas.SetLeft(SizeHandle, left + width - HandleSize / 2);
        Canvas.SetTop(SizeHandle, top + height - HandleSize / 2);
    }

    /// <summary>Shows the Page, unless another one is wanted meanwhile.</summary>
    private async Task ShowPageAsync(Document document, int pageIndex)
    {
        PagePicture.Source = null;
        var source = await PageImages.RenderAsync(document, pageIndex, PageRenderSize, PageRenderSize);
        if (_pageShown == (document, pageIndex))
        {
            PagePicture.Source = source;
        }
    }

    /// <summary>Shows the signature, its paper transparent, unless another one was chosen meanwhile.</summary>
    private async Task ShowSignatureImageAsync(ImageFile? signature)
    {
        SignaturePreview.Source = null;
        SignatureOnPage.Source = null;
        if (signature is null)
        {
            return;
        }

        var source = await PageImages.RenderSignatureAsync(_session, SignatureRenderWidth);
        if (ReferenceEquals(signature, _signatureShown))
        {
            SignaturePreview.Source = source;
            SignatureOnPage.Source = source;
        }
    }
}
