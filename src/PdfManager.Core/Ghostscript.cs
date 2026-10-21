using System.Diagnostics;
using System.Text;

namespace PdfManager.Core;

/// <summary>
/// The Ghostscript bundled next to the app (ADR-0001), run as a separate process to rewrite a PDF
/// with smaller images. Text, fonts, colours and the Pages' rotation stay as they are.
/// </summary>
internal static class Ghostscript
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Ghostscript");

    /// <summary>
    /// Rewrites <paramref name="inputFile"/> as <paramref name="outputFile"/>, both in
    /// <paramref name="workFolder"/>, bringing every colour and grey image down to at most
    /// <paramref name="dpi"/>.
    /// </summary>
    /// <param name="pageStarted">Told the number of each Page as Ghostscript starts on it.</param>
    public static async Task DownsampleImagesAsync(
        string workFolder, string inputFile, string outputFile, int dpi, Action<int>? pageStarted)
    {
        var start = new ProcessStartInfo(Path.Combine(Folder, "gswin64c.exe"))
        {
            // Plain file names inside the work folder, so a user name with umlauts in the temp path can't trip Ghostscript up.
            WorkingDirectory = workFolder,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // Only the bundled files, whichever Ghostscript is installed on this PC.
        start.Environment["GS_LIB"] = Folder;
        foreach (var argument in Arguments(inputFile, outputFile, dpi))
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        var errors = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is { } line && line.StartsWith("Page ", StringComparison.Ordinal) && int.TryParse(line[5..], out var page))
            {
                pageStarted?.Invoke(page);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            lock (errors)
            {
                errors.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Ghostscript stopped with exit code {process.ExitCode}: {errors}");
        }
    }

    private static IEnumerable<string> Arguments(string inputFile, string outputFile, int dpi) =>
    [
        "-dBATCH", "-dNOPAUSE", "-dSAFER",
        "-sDEVICE=pdfwrite",
        // Never grey or black-and-white, and every Page turned exactly as before.
        "-sColorConversionStrategy=LeaveColorUnchanged",
        "-dAutoRotatePages=/None",
        "-dDownsampleColorImages=true", "-dColorImageDownsampleType=/Bicubic",
        $"-dColorImageResolution={dpi}", "-dColorImageDownsampleThreshold=1.0",
        "-dDownsampleGrayImages=true", "-dGrayImageDownsampleType=/Bicubic",
        $"-dGrayImageResolution={dpi}", "-dGrayImageDownsampleThreshold=1.0",
        "-dDetectDuplicateImages=true",
        $"-sOutputFile={outputFile}",
        // JPEG at Acrobat's medium quality: Ghostscript's own default blurs small print in scans.
        "-c", "<< /ColorACSImageDict << /QFactor 0.76 /Blend 1 /HSamples [2 1 1 2] /VSamples [2 1 1 2] >>"
            + " /GrayACSImageDict << /QFactor 0.76 /Blend 1 /HSamples [2 1 1 2] /VSamples [2 1 1 2] >> >> setdistillerparams",
        "-f", inputFile,
    ];
}
