using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace OfficeBuddy.App.Controls;

/// <summary>
/// Lays out start-screen tiles in equal columns, giving every tile the height of the tallest one
/// so no description is cut off, whatever the Windows text size. When the tiles would not fit into
/// <see cref="FitHeight"/>, it uses more, narrower columns before leaving the rest to scrolling.
/// The first <see cref="FavoriteCount"/> tiles get rows of their own, centered, above the others.
/// </summary>
public sealed partial class TileGrid : Panel
{
    private const double ComfortableItemWidth = 280;
    // Narrower than this, long tile names such as "Zusammenfügen" break mid-word; scrolling is better.
    private const double NarrowestItemWidth = 260;
    private const double MinItemHeight = 140;
    private const double Spacing = 16;
    // Between the favorites and the other tiles, so they read as two groups.
    private const double GroupSpacing = 40;

    private double _fitHeight = double.PositiveInfinity;
    private int _favoriteCount;
    private Layout _layout;

    /// <summary>The height the tiles should fit into without scrolling.</summary>
    public double FitHeight
    {
        get => _fitHeight;
        set
        {
            if (value != _fitHeight)
            {
                _fitHeight = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>How many of the first tiles are favorites.</summary>
    public int FavoriteCount
    {
        get => _favoriteCount;
        set
        {
            if (value != _favoriteCount)
            {
                _favoriteCount = value;
                InvalidateMeasure();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? ComfortableItemWidth : availableSize.Width;

        var columns = Math.Max(1, (int)((width + Spacing) / (ComfortableItemWidth + Spacing)));
        _layout = MeasureWith(width, columns);
        while (_layout.Height > FitHeight && columns < Children.Count && ItemWidth(width, columns + 1) >= NarrowestItemWidth)
        {
            columns++;
            _layout = MeasureWith(width, columns);
        }

        return new Size(width, _layout.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var favorites = Math.Min(FavoriteCount, Children.Count);
        var top = ArrangeGroup(0, favorites, 0, finalSize.Width, centered: true);
        ArrangeGroup(favorites, Children.Count, top + (favorites > 0 ? GroupSpacing : 0), finalSize.Width, centered: false);

        return finalSize;
    }

    /// <summary>Arranges children <paramref name="start"/> to <paramref name="end"/> in rows from <paramref name="top"/>; returns the height they take.</summary>
    private double ArrangeGroup(int start, int end, double top, double width, bool centered)
    {
        for (var i = start; i < end; i++)
        {
            var column = (i - start) % _layout.Columns;
            var row = (i - start) / _layout.Columns;
            var rowStart = start + row * _layout.Columns;
            var tilesInRow = Math.Min(_layout.Columns, end - rowStart);
            var indent = centered ? (width - RowWidth(tilesInRow)) / 2 : 0;
            var x = indent + column * (_layout.ItemWidth + Spacing);
            var y = top + row * (_layout.ItemHeight + Spacing);
            Children[i].Arrange(new Rect(x, y, _layout.ItemWidth, _layout.ItemHeight));
        }

        return top + GroupHeight(end - start, _layout.Columns, _layout.ItemHeight);
    }

    private double RowWidth(int tiles) => tiles * _layout.ItemWidth + (tiles - 1) * Spacing;

    private Layout MeasureWith(double width, int columns)
    {
        var itemWidth = ItemWidth(width, columns);
        var itemHeight = MinItemHeight;
        foreach (var child in Children)
        {
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            itemHeight = Math.Max(itemHeight, child.DesiredSize.Height);
        }

        var favorites = Math.Min(FavoriteCount, Children.Count);
        var others = Children.Count - favorites;
        var height = GroupHeight(favorites, columns, itemHeight) + GroupHeight(others, columns, itemHeight)
            + (favorites > 0 && others > 0 ? GroupSpacing : 0);
        return new Layout(columns, itemWidth, itemHeight, height);
    }

    private static double GroupHeight(int tiles, int columns, double itemHeight)
    {
        var rows = (int)Math.Ceiling(tiles / (double)columns);
        return rows * itemHeight + Math.Max(0, rows - 1) * Spacing;
    }

    private static double ItemWidth(double width, int columns) => (width - (columns - 1) * Spacing) / columns;

    private readonly record struct Layout(int Columns, double ItemWidth, double ItemHeight, double Height);
}
