using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OfficeBuddy.App.Services;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

public sealed partial class ResizeImagePage : Page
{
    private readonly ResizeImageSession _session = new();
    private ImageFile? _thumbnailOf;
    // While the boxes are filled from the session, their changes are not the user's.
    private bool _isRefreshing;

    public ResizeImagePage()
    {
        InitializeComponent();
        // Here rather than in XAML: Checked would come before the boxes it enables exist.
        ByPercentage.IsChecked = true;
        Shell.Title = Strings.Get("ResizeImageTitle");
        Shell.Opens = OpenableFiles.Images;
        Shell.Session = _session;
        _session.Changed += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
    }

    public double ThumbnailSize => 160;

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

    private Task<bool> OpenAsync(IReadOnlyList<string> paths) => Shell.OpenOneAsync(paths, _session.OpenAsync);

    private void Mode_Checked(object sender, RoutedEventArgs e) => UpdateInputs();

    private void PercentageBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
        OnTyped(args.NewValue, value => _session.SetPercentage(value));

    private void WidthBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
        OnTyped(args.NewValue, value => _session.SetWidth((int)Math.Round(value), KeepProportions.IsChecked == true));

    private void HeightBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
        OnTyped(args.NewValue, value => _session.SetHeight((int)Math.Round(value), KeepProportions.IsChecked == true));

    private void OnTyped(double value, Action<double> apply)
    {
        if (_isRefreshing || _session.Image is null)
        {
            return;
        }

        if (!double.IsNaN(value))
        {
            apply(value);
        }
        // An emptied box, or a value the session rounded or limited, shows the actual size again.
        DispatcherQueue.TryEnqueue(Refresh);
    }

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
        ImageArea.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = _session.CanSave;
        if (image is null)
        {
            return;
        }

        if (!ReferenceEquals(image, _thumbnailOf))
        {
            _thumbnailOf = image;
            _ = ShowThumbnailAsync(image);
        }
        var (original, size) = (_session.OriginalSize!, _session.Size!);
        NameText.Text = image.FileName;
        OriginalSizeText.Text = Strings.Format("ResizeOriginalSize", original.Width, original.Height);
        NewSizeText.Text = Strings.Format("ResizeNewSize", size.Width, size.Height);

        _isRefreshing = true;
        // Empty while width and height are not in proportion: no single percentage describes that.
        PercentageBox.Value = _session.Percentage is { } percentage ? Math.Round(percentage, 1) : double.NaN;
        WidthBox.Value = size.Width;
        HeightBox.Value = size.Height;
        _isRefreshing = false;
        UpdateInputs();
    }

    /// <summary>Only the boxes of the chosen way to give the size can be changed.</summary>
    private void UpdateInputs()
    {
        var byPercentage = ByPercentage.IsChecked == true;
        PercentageBox.IsEnabled = byPercentage;
        WidthBox.IsEnabled = !byPercentage;
        HeightBox.IsEnabled = !byPercentage;
        KeepProportions.IsEnabled = !byPercentage;
    }

    /// <summary>Shows the image the right way up, unless another one was opened meanwhile.</summary>
    private async Task ShowThumbnailAsync(ImageFile image)
    {
        Thumbnail.Source = null;
        ThumbnailPlaceholder.Visibility = Visibility.Visible;
        var source = await PageImages.RenderAsync(image, ThumbnailSize, ThumbnailSize);
        if (ReferenceEquals(image, _thumbnailOf) && source is not null)
        {
            Thumbnail.Source = source;
            ThumbnailPlaceholder.Visibility = Visibility.Collapsed;
        }
    }
}
