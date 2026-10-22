using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace OfficeBuddy.App.Controls;

/// <summary>
/// Lays out start-screen tiles in groups, each under its heading, in equal columns shared by all
/// groups, giving every tile the height of the tallest one so no description is cut off, whatever
/// the Windows text size. When it all would not fit into <see cref="FitHeight"/>, it uses more,
/// narrower columns before leaving the rest to scrolling.
/// </summary>
public sealed partial class TileGrid : Panel
{
    private const double ComfortableItemWidth = 280;
    // Narrower than this, long tile names such as "Zusammenfügen" break mid-word; scrolling is better.
    private const double NarrowestItemWidth = 260;
    private const double MinItemHeight = 140;
    private const double Spacing = 16;
    // Below a heading, and above the next one, so each group reads as one.
    private const double HeadingSpacing = 10;
    private const double GroupSpacing = 32;

    private double _fitHeight = double.PositiveInfinity;
    private IReadOnlySet<int> _headings = new HashSet<int>();
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

    /// <summary>The positions of the children that are group headings; every other child is a tile.</summary>
    public IReadOnlySet<int> Headings
    {
        get => _headings;
        set
        {
            if (!value.SetEquals(_headings))
            {
                _headings = value;
                InvalidateMeasure();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? ComfortableItemWidth : availableSize.Width;
        var tiles = Children.Count - _headings.Count(index => index < Children.Count);

        var columns = Math.Max(1, (int)((width + Spacing) / (ComfortableItemWidth + Spacing)));
        _layout = MeasureWith(width, columns);
        while (_layout.Height > FitHeight && columns < tiles && ItemWidth(width, columns + 1) >= NarrowestItemWidth)
        {
            columns++;
            _layout = MeasureWith(width, columns);
        }

        return new Size(width, _layout.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Place(finalSize.Width, (child, rect) => child.Arrange(rect));
        return finalSize;
    }

    private Layout MeasureWith(double width, int columns)
    {
        var itemWidth = ItemWidth(width, columns);
        var itemHeight = MinItemHeight;
        for (var i = 0; i < Children.Count; i++)
        {
            if (_headings.Contains(i))
            {
                Children[i].Measure(new Size(width, double.PositiveInfinity));
            }
            else
            {
                Children[i].Measure(new Size(itemWidth, double.PositiveInfinity));
                itemHeight = Math.Max(itemHeight, Children[i].DesiredSize.Height);
            }
        }

        _layout = new Layout(columns, itemWidth, itemHeight, 0);
        return _layout with { Height = Place(width, arrange: null) };
    }

    /// <summary>
    /// Goes through the children in order: a heading across the full width, then its group's tiles
    /// in rows. Arranges them if asked, and returns the height it all takes.
    /// </summary>
    private double Place(double width, Action<UIElement, Rect>? arrange)
    {
        var (top, column, rowOpen) = (0.0, 0, false);
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (_headings.Contains(i))
            {
                if (rowOpen)
                {
                    top += _layout.ItemHeight;
                    rowOpen = false;
                }
                if (i > 0)
                {
                    top += GroupSpacing;
                }

                arrange?.Invoke(child, new Rect(0, top, width, child.DesiredSize.Height));
                top += child.DesiredSize.Height + HeadingSpacing;
                column = 0;
                continue;
            }

            if (column == _layout.Columns)
            {
                top += _layout.ItemHeight + Spacing;
                column = 0;
            }
            arrange?.Invoke(child, new Rect(column * (_layout.ItemWidth + Spacing), top, _layout.ItemWidth, _layout.ItemHeight));
            column++;
            rowOpen = true;
        }

        return rowOpen ? top + _layout.ItemHeight : top;
    }

    private static double ItemWidth(double width, int columns) => (width - (columns - 1) * Spacing) / columns;

    private readonly record struct Layout(int Columns, double ItemWidth, double ItemHeight, double Height);
}
