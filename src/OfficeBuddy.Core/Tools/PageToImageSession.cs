using OfficeBuddy.Core.Files;

namespace OfficeBuddy.Core.Tools;

/// <summary>The state of the Page to image Tool: one Document and the Pages selected to be saved as JPG images.</summary>
public sealed class PageToImageSession : PageSelectionSession
{
    // Sharp enough to read and print a Page.
    private const int Dpi = 200;

    /// <summary>
    /// The names of the files a save with this base name writes, e.g. <c>Rechnung - Seite 2.jpg</c>
    /// with the original page number, so existing ones can be checked first.
    /// </summary>
    public IReadOnlyList<string> FileNamesFor(string baseName) =>
        [.. State.Select(index => $"{baseName} - Seite {index + 1}.jpg")];

    /// <summary>
    /// Saves every selected Page as a JPG image into <paramref name="folder"/>, all or nothing.
    /// </summary>
    /// <param name="imagesWritten">Told how many images have been written so far.</param>
    /// <returns>The paths of the images written, in page order.</returns>
    public async Task<IReadOnlyList<string>> SaveAsync(string folder, string baseName, IProgress<int>? imagesWritten = null)
    {
        var (document, selected) = (Document!, State);
        var written = await SeveralFiles.WriteAsync(folder, FileNamesFor(baseName), async (i, path) =>
        {
            await File.WriteAllBytesAsync(path, await document.RenderPageAsJpegAsync(selected[i], Dpi));
            imagesWritten?.Report(i + 1);
        });
        MarkSaved(selected);
        return written;
    }
}
