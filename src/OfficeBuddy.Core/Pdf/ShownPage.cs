using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OfficeBuddy.Core.Pdf;

/// <summary>The size of a Page as readers show it, in points (1/72 inch).</summary>
public sealed record PageDimensions(double Width, double Height);

/// <summary>
/// A Page as readers show it: its visible area (the crop box, else the media box), turned by the
/// Page's own rotation. Tools that draw on a Page place things where the user saw them.
/// </summary>
internal static class ShownPage
{
    public static PageDimensions Size(PdfPage page)
    {
        var (box, rotation) = Read(page);
        return rotation is 90 or 270 ? new PageDimensions(box.Height, box.Width) : new PageDimensions(box.Width, box.Height);
    }

    /// <summary>
    /// For an <see cref="XGraphics"/> on the Page: makes drawing coordinates points from the top left
    /// corner of the Page as shown, x to the right and y down, whatever its rotation.
    /// </summary>
    public static XMatrix Transform(PdfPage page)
    {
        var (box, rotation) = Read(page);
        // XGraphics draws on the unturned Page, y measured down from the top of its media box,
        // whatever the Page's rotation or where its boxes start.
        var height = page.Height.Point;

        // Where a point of the shown Page lies in PDF space (y up), for each rotation clockwise.
        XPoint Raw(double x, double y) => rotation switch
        {
            90 => new XPoint(box.X1 + y, box.Y1 + x),
            180 => new XPoint(box.X2 - x, box.Y1 + y),
            270 => new XPoint(box.X2 - y, box.Y2 - x),
            _ => new XPoint(box.X1 + x, box.Y2 - y),
        };
        XPoint OnGraphics(double x, double y)
        {
            var raw = Raw(x, y);
            return new XPoint(raw.X, height - raw.Y);
        }

        var (origin, right, down) = (OnGraphics(0, 0), OnGraphics(1, 0), OnGraphics(0, 1));
        return new XMatrix(right.X - origin.X, right.Y - origin.Y, down.X - origin.X, down.Y - origin.Y, origin.X, origin.Y);
    }

    private static (PdfRectangle Box, int Rotation) Read(PdfPage page)
    {
        // Read from the Page's entries: PDFsharp's CropBox property adds an empty crop box if there is none.
        var media = page.MediaBox;
        var crop = page.Elements.ContainsKey("/CropBox") ? page.Elements.GetRectangle("/CropBox") : media;
        var box = crop.IsZero ? media : crop;
        return (box, (page.Rotate % 360 + 360) % 360);
    }
}
