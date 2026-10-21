using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace PdfManager.App.Controls;

/// <summary>
/// Lays out start-screen tiles in equal columns, giving every tile the height of the tallest one
/// so no description is cut off, whatever the Windows text size. When the tiles would not fit into
/// <see cref="FitHeight"/>, it uses more, narrower columns before leaving the rest to scrolling.
/// </summary>
public sealed partial class TileGrid : Panel
{
    private const double ComfortableItemWidth = 280;
    // Narrower than this, long tile names such as "Zusammenfügen" break mid-word; scrolling is better.
    private const double NarrowestItemWidth = 260;
    private const double MinItemHeight = 140;
    private const double Spacing = 16;

    private double _fitHeight = double.PositiveInfinity;
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
        for (var i = 0; i < Children.Count; i++)
        {
            var x = i % _layout.Columns * (_layout.ItemWidth + Spacing);
            var y = i / _layout.Columns * (_layout.ItemHeight + Spacing);
            Children[i].Arrange(new Rect(x, y, _layout.ItemWidth, _layout.ItemHeight));
        }

        return finalSize;
    }

    private Layout MeasureWith(double width, int columns)
    {
        var itemWidth = ItemWidth(width, columns);
        var itemHeight = MinItemHeight;
        foreach (var child in Children)
        {
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            itemHeight = Math.Max(itemHeight, child.DesiredSize.Height);
        }

        var rows = (int)Math.Ceiling(Children.Count / (double)columns);
        return new Layout(columns, itemWidth, itemHeight, rows * itemHeight + Math.Max(0, rows - 1) * Spacing);
    }

    private static double ItemWidth(double width, int columns) => (width - (columns - 1) * Spacing) / columns;

    private readonly record struct Layout(int Columns, double ItemWidth, double ItemHeight, double Height);
}
