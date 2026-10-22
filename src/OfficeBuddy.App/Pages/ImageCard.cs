using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using OfficeBuddy.Core.Images;

namespace OfficeBuddy.App.Pages;

/// <summary>
/// One image in the list of Images to PDF or Images side by side, at its current position, with a
/// line about it such as which way round its Page will be.
/// </summary>
public sealed class ImageCard(ImageFile image, int index, string detail) : INotifyPropertyChanged
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

    public string Name => Image.FileName;

    public string Detail { get; } = detail;

    public string RemoveLabel => Strings.Get("RemoveFromList");
}
