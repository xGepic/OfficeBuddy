using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using OfficeBuddy.Core;
using Windows.Storage.Streams;

namespace OfficeBuddy.App.Services;

/// <summary>
/// Pages and images as pictures on screen: rendered in the background, shown sharp at the display's
/// scaling.
/// </summary>
internal static class PageImages
{
    /// <summary>
    /// Renders a Page (by its index in the Document) to fit a box of the given size in effective
    /// pixels. Call on the UI thread. Null if Windows cannot draw this Page.
    /// </summary>
    public static Task<ImageSource?> RenderAsync(Document document, int pageIndex, double width, double height) =>
        ShowAsync((maxWidth, maxHeight) => document.RenderPageAsPngAsync(pageIndex, maxWidth, maxHeight), width, height);

    /// <summary>
    /// Renders an image the right way up to fit a box of the given size in effective pixels. Call on
    /// the UI thread. Null if Windows cannot draw it.
    /// </summary>
    public static Task<ImageSource?> RenderAsync(ImageFile image, double width, double height) =>
        ShowAsync(image.RenderPngAsync, width, height);

    /// <summary>
    /// Renders the image Images side by side would save now, to fit a box of the given size in
    /// effective pixels. Call on the UI thread. Null if Windows cannot draw it.
    /// </summary>
    public static Task<ImageSource?> RenderAsync(ImagesSideBySideSession session, double width, double height) =>
        ShowAsync(session.RenderPreviewPngAsync, width, height);

    private static async Task<ImageSource?> ShowAsync(Func<int, int, Task<byte[]>> renderPng, double width, double height)
    {
        var scale = App.MainWindow.Content?.XamlRoot?.RasterizationScale ?? 1.0;
        try
        {
            var png = await Task.Run(() => renderPng((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale)));

            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(png.AsBuffer());
            stream.Seek(0);
            var image = new BitmapImage();
            await image.SetSourceAsync(stream);
            return image;
        }
        catch (Exception)
        {
            // Windows' renderer may reject a file PDFsharp could open; the screen shows a placeholder instead.
            return null;
        }
    }
}
