using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using OfficeBuddy.Core;

namespace OfficeBuddy.App.Controls;

/// <summary>
/// One Page in a <see cref="PageGrid"/>: its thumbnail, page number, rotation, removal mark,
/// whether it is selected, and in a Split its Part and the gap after it.
/// </summary>
public sealed class PageThumbnail(int sourceIndex) : INotifyPropertyChanged
{
    private ImageSource? _image;
    private int _rotation;
    private bool _isMarkedForRemoval;
    private bool _isSelected;
    private SplitMark? _split;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SourceIndex { get; } = sourceIndex;

    public string PageNumberText { get; } = Strings.Format("PageNumber", sourceIndex + 1);

    public ImageSource? Image
    {
        get => _image;
        set
        {
            _image = value;
            OnPropertyChanged(nameof(Image));
            OnPropertyChanged(nameof(PlaceholderVisibility));
        }
    }

    public Visibility PlaceholderVisibility => _image is null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The rotation still to be saved, in degrees clockwise.</summary>
    public double Rotation => _rotation;

    public double ImageOpacity => _isMarkedForRemoval ? 0.35 : 1.0;

    public Visibility RemovalMarkVisibility => _isMarkedForRemoval ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value != _isSelected)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(SelectionBadgeVisibility));
            }
        }
    }

    public Visibility SelectionBadgeVisibility => _isSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PartBandVisibility => _split is null ? Visibility.Collapsed : Visibility.Visible;

    public Brush? PartBrush => _split?.PartBrush;

    /// <summary>"Teil 2" on the first Page of a Part, empty on the others.</summary>
    public string PartLabel => _split is { StartsPart: true } split ? Strings.Format("PartLabel", split.PartNumber) : "";

    public Visibility GapVisibility => _split is { HasGapAfter: true } ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ScissorsVisibility => _split is { SplitAfter: true } ? Visibility.Visible : Visibility.Collapsed;

    public double GapLineOpacity => _split is { SplitAfter: true } ? 1.0 : 0.35;

    public string GapLabel => Strings.Format(
        _split is { SplitAfter: true } ? "GapRemoveSplit" : "GapAddSplit", SourceIndex + 1, SourceIndex + 2);

    public string AccessibleName =>
        _isMarkedForRemoval ? Strings.Format("PageMarkedForRemoval", SourceIndex + 1) : PageNumberText;

    public void Update(PageEntry page)
    {
        if (page.Rotation != _rotation)
        {
            _rotation = page.Rotation;
            OnPropertyChanged(nameof(Rotation));
        }

        if (page.IsMarkedForRemoval != _isMarkedForRemoval)
        {
            _isMarkedForRemoval = page.IsMarkedForRemoval;
            OnPropertyChanged(nameof(ImageOpacity));
            OnPropertyChanged(nameof(RemovalMarkVisibility));
            OnPropertyChanged(nameof(AccessibleName));
        }
    }

    /// <summary>Shows this Page as part of a Split, or (with null) not at all.</summary>
    public void ShowSplit(SplitMark? split)
    {
        if (split == _split)
        {
            return;
        }

        _split = split;
        foreach (var name in new[]
        {
            nameof(PartBandVisibility), nameof(PartBrush), nameof(PartLabel), nameof(GapVisibility),
            nameof(ScissorsVisibility), nameof(GapLineOpacity), nameof(GapLabel),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// How a Page shows in a Split: its Part's number and colour, whether it starts that Part, whether
/// there is a gap after it to click, and whether a Split point sits in that gap.
/// </summary>
public sealed record SplitMark(int PartNumber, Brush PartBrush, bool StartsPart, bool HasGapAfter, bool SplitAfter);

/// <summary>A Page dragged from one position to another.</summary>
public sealed record PageMove(int From, int To);
