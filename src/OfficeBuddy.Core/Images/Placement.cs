namespace OfficeBuddy.Core.Images;

/// <summary>
/// Where a picture sits on another one, such as the Inserted image on the Main image or a signature
/// on a Page: its top left corner and its width, each as a share of the other's width or height,
/// from 0 to 1. Its height follows from its proportions.
/// </summary>
public sealed record Placement(double Left, double Top, double Width)
{
    /// <summary>
    /// The place nearest to this one where a picture taking <paramref name="heightPerWidth"/> shares
    /// of the height per share of the width lies whole on the other, at least
    /// <paramref name="minWidth"/> wide where there is room for that.
    /// </summary>
    internal Placement Fit(double heightPerWidth, double minWidth)
    {
        var maxWidth = Math.Min(1, 1 / heightPerWidth);
        var width = Math.Clamp(Width, Math.Min(minWidth, maxWidth), maxWidth);
        var height = width * heightPerWidth;
        return new Placement(Math.Clamp(Left, 0, 1 - width), Math.Clamp(Top, 0, 1 - height), width);
    }
}
