using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OfficeBuddy.Core.Tests.Support;

public static partial class SamplePdfs
{
    /// <summary>
    /// A scanned letter: one Page that is only a picture of these lines of text, as a scanner makes
    /// it, with no text in the Document itself.
    /// </summary>
    public static async Task<string> CreateTextScanAsync(string folder, string fileName, params string[] lines)
    {
        // The letter as a Document with real text, then a 200 dpi picture of it made into a new one.
        var typed = Path.Combine(folder, $"~{Guid.NewGuid():N}.pdf");
        using (var document = new PdfDocument())
        {
            var page = document.AddPage();
            page.Size = PageSize.A4;
            using var graphics = XGraphics.FromPdfPage(page);
            var font = new XFont("Arial", 18, XFontStyleEx.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
            for (var i = 0; i < lines.Length; i++)
            {
                graphics.DrawString(lines[i], font, XBrushes.Black, 70, 120 + i * 36);
            }
            document.Save(typed);
        }

        var picture = Path.Combine(folder, $"~{Guid.NewGuid():N}.jpg");
        var loaded = await Document.LoadAllAsync([typed]);
        await File.WriteAllBytesAsync(picture, await loaded.Documents[0].RenderPageAsJpegAsync(0, 200));

        var scan = new ImagesToPdfSession();
        await scan.AddAsync([picture]);
        var path = Path.Combine(folder, fileName);
        await scan.SaveAsync(path);
        File.Delete(typed);
        File.Delete(picture);
        return path;
    }
}
