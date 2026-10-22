using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.App.Services;
using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Images;
using OfficeBuddy.Core.Pdf;
using OfficeBuddy.Core.Tools;

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

    public SignPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("SignTitle");
        Shell.Session = _session;
        foreach (var (button, key) in new[] { (PreviousButton, "SignPreviousPage"), (NextButton, "SignNextPage") })
        {
            AutomationProperties.SetName(button, Strings.Get(key));
            ToolTipService.SetToolTip(button, Strings.Get(key));
        }
        _session.Changed += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
    }

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

    private void Stage_Placed(object? sender, Placement placement)
    {
        _session.Place(_pageIndex, placement);
        ShowStage();
    }

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
        ShowStage();
    }

    /// <summary>Shows the Page as it is shown, with the signature where it is put on it.</summary>
    private void ShowStage()
    {
        if (_session.Document is not { } document)
        {
            return;
        }

        var size = document.ShownPageSize(_pageIndex);
        var placement = PlacementHere;
        Stage.Show(size.Width, size.Height, placement, placement is null ? 0 : _session.HeightFor(_pageIndex, 1));
    }

    /// <summary>Shows the Page, unless another one is wanted meanwhile.</summary>
    private async Task ShowPageAsync(Document document, int pageIndex)
    {
        Stage.Picture = null;
        var source = await PageImages.RenderAsync(document, pageIndex, PageRenderSize, PageRenderSize);
        if (_pageShown == (document, pageIndex))
        {
            Stage.Picture = source;
        }
    }

    /// <summary>Shows the signature, its paper transparent, unless another one was chosen meanwhile.</summary>
    private async Task ShowSignatureImageAsync(ImageFile? signature)
    {
        SignaturePreview.Source = null;
        Stage.PlacedPicture = null;
        if (signature is null)
        {
            return;
        }

        var source = await PageImages.RenderSignatureAsync(_session, SignatureRenderWidth);
        if (ReferenceEquals(signature, _signatureShown))
        {
            SignaturePreview.Source = source;
            Stage.PlacedPicture = source;
        }
    }
}
