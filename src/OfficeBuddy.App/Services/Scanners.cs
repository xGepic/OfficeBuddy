using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Storage;

namespace OfficeBuddy.App.Services;

/// <summary>Where the paper lies: as the scanner decides, on the glass, or in the document feeder.</summary>
internal enum ScanSource
{
    Automatic,
    Flatbed,
    Feeder,
}

/// <summary>A scanner connected to the PC.</summary>
internal sealed record ScannerInfo(string Id, string Name, bool HasFlatbed, bool HasFeeder);

/// <summary>
/// The scanners Windows knows, through its own scanner support (the same as the Windows Scan app),
/// with no driver software of our own.
/// </summary>
internal static class Scanners
{
    // Sharp enough to read small print and to recognise text, small enough for email.
    private const uint Dpi = 200;

    /// <summary>The connected scanners; none if Windows can't tell.</summary>
    public static async Task<IReadOnlyList<ScannerInfo>> FindAsync()
    {
        var found = new List<ScannerInfo>();
        try
        {
            foreach (var device in await DeviceInformation.FindAllAsync(ImageScanner.GetDeviceSelector()))
            {
                try
                {
                    var scanner = await ImageScanner.FromIdAsync(device.Id);
                    found.Add(new ScannerInfo(device.Id, device.Name,
                        scanner.IsScanSourceSupported(ImageScannerScanSource.Flatbed),
                        scanner.IsScanSourceSupported(ImageScannerScanSource.Feeder)));
                }
                catch (Exception)
                {
                    // Switched off or busy: not offered.
                }
            }
        }
        catch (Exception)
        {
            // Device search unavailable: no scanners.
        }
        return found;
    }

    /// <summary>
    /// Scans into <paramref name="folder"/>: one picture from the glass, or one per sheet in the
    /// feeder. Colour JPGs at 200 dpi where the scanner lets us choose.
    /// </summary>
    /// <returns>The paths of the scanned pictures, in order.</returns>
    /// <exception cref="Exception">The scanner didn't scan, e.g. no paper in the feeder or it is switched off.</exception>
    public static async Task<IReadOnlyList<string>> ScanAsync(ScannerInfo info, ScanSource source, string folder)
    {
        var scanner = await ImageScanner.FromIdAsync(info.Id);
        var scanSource = source switch
        {
            ScanSource.Flatbed => ImageScannerScanSource.Flatbed,
            ScanSource.Feeder => ImageScannerScanSource.Feeder,
            _ => ImageScannerScanSource.Default,
        };

        if (source == ScanSource.Flatbed)
        {
            Configure(scanner.FlatbedConfiguration);
        }
        else if (source == ScanSource.Feeder)
        {
            Configure(scanner.FeederConfiguration);
            // Every sheet in the feeder.
            scanner.FeederConfiguration.MaxNumberOfPages = 0;
        }

        var result = await scanner.ScanFilesToFolderAsync(scanSource, await StorageFolder.GetFolderFromPathAsync(folder));
        return [.. result.ScannedFiles.Select(file => file.Path)];
    }

    private static void Configure(IImageScannerSourceConfiguration configuration)
    {
        if (configuration.IsFormatSupported(ImageScannerFormat.Jpeg))
        {
            configuration.Format = ImageScannerFormat.Jpeg;
        }
        if (configuration.IsColorModeSupported(ImageScannerColorMode.Color))
        {
            configuration.ColorMode = ImageScannerColorMode.Color;
        }
        configuration.DesiredResolution = new ImageScannerResolution { DpiX = Dpi, DpiY = Dpi };
    }
}
