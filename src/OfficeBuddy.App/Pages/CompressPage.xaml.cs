using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.App.Services;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

public sealed partial class CompressPage : Page
{
    private readonly CompressSession _session = new();
    private Document? _thumbnailOf;

    public CompressPage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("CompressTitle");
        Shell.Session = _session;
        _session.Changed += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
    }

    public double ThumbnailWidth => 120;

    public double ThumbnailHeight => 160;

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

    private async void Compress_Click(object sender, RoutedEventArgs e)
    {
        CompressButton.IsEnabled = false;
        var progress = new Progress<CompressProgress>(at => Shell.UpdateBusyMessage(Strings.Format(
            at.PlanningParts ? "BusyPlanningParts" : at.Step == 1 ? "BusyCompressing" : "BusyCompressingMore",
            at.Page, at.PageCount)));
        try
        {
            using (Shell.ShowBusy(Strings.Get("BusyCompressingStarted")))
            {
                await _session.CompressAsync(progress);
            }
        }
        catch (Exception)
        {
            // Ghostscript is missing, could not read the Document, or there is no room for its temporary files.
            Shell.ShowError(Strings.Format("CompressFailed", _session.Document!.FileName));
        }
        Refresh();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        await Shell.SaveToFileAsync(_session.SuggestedFolder, _session.SuggestedFileName!, _session.SaveAsync);
        Refresh();
    }

    private async void SaveAsParts_Click(object sender, RoutedEventArgs e)
    {
        SaveAsPartsButton.IsEnabled = false;
        await Shell.SaveToFolderAsync(
            _session.SuggestedFolder, _session.SuggestedBaseName!, FileFilter.Pdf, _session.PartFileNamesFor, _session.SaveAsPartsAsync);
        Refresh();
    }

    private void Refresh()
    {
        var document = _session.Document;
        EmptyState.Visibility = document is null ? Visibility.Visible : Visibility.Collapsed;
        DocumentArea.Visibility = document is null ? Visibility.Collapsed : Visibility.Visible;
        if (document is null)
        {
            return;
        }

        if (!ReferenceEquals(document, _thumbnailOf))
        {
            _thumbnailOf = document;
            _ = ShowThumbnailAsync(document);
        }
        NameText.Text = document.FileName;
        InfoText.Text = $"{Strings.PageCount(document.PageCount)} · {Strings.FileSize(_session.OriginalSize)}";

        var result = _session.Result;
        StatusText.Text = Strings.Format(
            _session.IsAlreadySmallEnough ? "CompressAlreadySmall" : "CompressTooLarge", Strings.FileSize(_session.OriginalSize));
        StatusText.Visibility = result is null ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = result is null ? Visibility.Collapsed : Visibility.Visible;
        if (result is not null)
        {
            SizesText.Text = Strings.Format("CompressSizes", Strings.FileSize(result.OriginalSize), Strings.FileSize(result.CompressedSize));
            ResultText.Text = _session.IsAlreadySmallEnough
                ? Strings.Get(result.IsSmaller ? "CompressReachedLightly" : "CompressNotSmaller")
                : result.ReachedTarget ? Strings.Get("CompressReached")
                : $"{Strings.Get("CompressNotReached")} {DescribeParts(result)}";
        }

        CompressButtonText.Text = Strings.Get(_session.IsAlreadySmallEnough ? "CompressLightly" : "CompressStart");
        AutomationProperties.SetName(CompressButton, CompressButtonText.Text);
        CompressButton.Visibility = result is null ? Visibility.Visible : Visibility.Collapsed;
        CompressButton.IsEnabled = _session.CanCompress;
        SaveButton.Visibility = result is null ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = _session.CanSave;
        // Splitting into Parts is what helps when the whole is too big; saving it whole stays possible.
        SaveAsPartsButton.Visibility = _session.CanSaveAsParts ? Visibility.Visible : Visibility.Collapsed;
        SaveAsPartsButton.IsEnabled = _session.CanSaveAsParts;
        SaveButton.Style = (Style)Application.Current.Resources[_session.CanSaveAsParts ? "DefaultButtonStyle" : "AccentButtonStyle"];
    }

    /// <summary>Into how many Parts the result can be split, or which Pages are too big for that.</summary>
    private static string DescribeParts(CompressResult result) => result.OversizedPageNumbers switch
    {
        [] => Strings.Format("CompressPartsPossible", result.PartCount),
        [var number] => Strings.Format("CompressPageTooLargeOne", number),
        var numbers => Strings.Format("CompressPageTooLargeMany", string.Join(", ", numbers)),
    };

    /// <summary>Shows the first Page of the Document, unless another one was opened meanwhile.</summary>
    private async Task ShowThumbnailAsync(Document document)
    {
        Thumbnail.Source = null;
        ThumbnailPlaceholder.Visibility = Visibility.Visible;
        var image = await PageImages.RenderAsync(document, 0, ThumbnailWidth, ThumbnailHeight);
        if (ReferenceEquals(document, _thumbnailOf) && image is not null)
        {
            Thumbnail.Source = image;
            ThumbnailPlaceholder.Visibility = Visibility.Collapsed;
        }
    }
}
