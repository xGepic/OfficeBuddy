using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace PdfManager.App.Services;

/// <summary>Files dragged in from File Explorer.</summary>
internal static class DroppedFiles
{
    /// <summary>If files are being dragged, accepts them as a copy and shows the caption next to the cursor.</summary>
    public static void AcceptIfOffered(DragEventArgs e, string caption)
    {
        if (!AreOffered(e))
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = caption;
        e.DragUIOverride.IsCaptionVisible = true;
        e.Handled = true;
    }

    /// <summary>The paths of the dropped files; empty if no files were dropped or they could not be read.</summary>
    public static async Task<IReadOnlyList<string>> GetPathsAsync(DragEventArgs e)
    {
        if (!AreOffered(e))
        {
            return [];
        }

        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            return items.OfType<StorageFile>().Select(file => file.Path).ToList();
        }
        catch (Exception)
        {
            // Drop handlers are async void: a source that cannot hand over its files must not crash the app.
            return [];
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static bool AreOffered(DragEventArgs e) => e.DataView.Contains(StandardDataFormats.StorageItems);
}
