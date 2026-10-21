using PdfSharp.Pdf;

namespace PdfManager.Core;

/// <summary>A Page for a new Document: a Page of an opened Document, rotated a further <paramref name="Rotation"/> degrees clockwise.</summary>
internal readonly record struct PlannedPage(Document Source, int PageIndex, int Rotation = 0);

/// <summary>Writes new Documents made of Pages from opened Documents. Every session saves through here.</summary>
internal static class DocumentWriter
{
    /// <summary>Builds the Document in the background, then writes it to <paramref name="path"/>.</summary>
    public static async Task WriteAsync(string path, IReadOnlyList<PlannedPage> pages)
    {
        var content = await Task.Run(() => Build(pages));
        await File.WriteAllBytesAsync(path, content);
    }

    private static byte[] Build(IEnumerable<PlannedPage> pages)
    {
        using var output = new PdfDocument();
        Document? currentSource = null;
        PdfDocument? source = null;
        try
        {
            foreach (var planned in pages)
            {
                // A fresh copy for each run of Pages from one Document, so the same Document can appear twice.
                if (!ReferenceEquals(planned.Source, currentSource))
                {
                    source?.Dispose();
                    source = planned.Source.OpenForImport();
                    currentSource = planned.Source;
                }

                var page = output.AddPage(source!.Pages[planned.PageIndex]);
                if (planned.Rotation != 0)
                {
                    // PDF allows negative rotations such as -90; save a value between 0 and 270.
                    page.Rotate = ((page.Rotate + planned.Rotation) % 360 + 360) % 360;
                }
            }
        }
        finally
        {
            source?.Dispose();
        }

        using var buffer = new MemoryStream();
        output.Save(buffer);
        return buffer.ToArray();
    }
}
