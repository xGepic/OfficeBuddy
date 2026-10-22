using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
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

    public PictureInPicturePage()
    {
        InitializeComponent();
        Shell.Title = Strings.Get("PictureInPictureTitle");
        Shell.Opens = OpenableFiles.Images;
        Shell.Session = _session;
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

    private void Stage_Placed(object? sender, Placement placement)
    {
        _session.Place(placement);
        ShowStage();
    }

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
        ShowStage();
    }

    /// <summary>Shows the photo with the Inserted image where it is put on it.</summary>
    private void ShowStage()
    {
        if (_session.MainImage is not { } main)
        {
            return;
        }

        var placement = _session.Inserted?.Placement;
        Stage.Show(main.Width, main.Height, placement, placement is null ? 0 : _session.HeightFor(1));
    }

    /// <summary>Shows the photo the right way up, unless another one was opened meanwhile.</summary>
    private async Task ShowMainAsync(ImageFile main)
    {
        Stage.Picture = null;
        var source = await PageImages.RenderAsync(main, MainRenderWidth, MainRenderHeight);
        if (ReferenceEquals(main, _mainShown))
        {
            Stage.Picture = source;
        }
    }

    /// <summary>Shows the Inserted image the right way up, unless another one was inserted meanwhile.</summary>
    private async Task ShowInsertedAsync(ImageFile image)
    {
        Stage.PlacedPicture = null;
        var source = await PageImages.RenderAsync(image, InsertedRenderSize, InsertedRenderSize);
        if (ReferenceEquals(image, _insertedShown))
        {
            Stage.PlacedPicture = source;
        }
    }
}
