using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Pages;

/// <summary>
/// One image in the list of Images to PDF, Images side by side or Scan, at its current position,
/// with a line about it such as which way round its Page will be, and a name other than the file's
/// if given (a scanned Page's file name means nothing to the user).
/// </summary>
public sealed class ImageCard(ImageFile image, int index, string detail, string? name = null) : INotifyPropertyChanged
{
    public const double ThumbnailSize = 64;

    private ImageSource? _thumbnail;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ImageFile Image { get; } = image;

    public int Index { get; } = index;

    /// <summary>The image the right way up, once it is drawn.</summary>
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

    public string Name => name ?? Image.FileName;

    public string Detail { get; } = detail;

    public string RemoveLabel => Strings.Get("RemoveFromList");

    public string RotateLabel => Strings.Get("ScanRotate");
}
