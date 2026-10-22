using System.Diagnostics;
using System.Text;

namespace OfficeBuddy.Core;

/// <summary>
/// The Ghostscript bundled next to the app (ADR-0001), run as a separate process to rewrite a PDF:
/// with smaller images, without its password, or rebuilt from a damaged file. Text, fonts, colours
/// and the Pages' rotation stay as they are.
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
    public static Task DownsampleImagesAsync(
        string workFolder, string inputFile, string outputFile, int dpi, Action<int>? pageStarted) =>
        RunAsync(workFolder, DownsampleArguments(inputFile, outputFile, dpi), pageStarted);

    /// <summary>
    /// Writes the PDF <paramref name="content"/> anew, as Ghostscript reads it, changing nothing on
    /// purpose. A damaged file comes out readable if Ghostscript can make sense of it, and a file
    /// opened with <paramref name="password"/> comes out without one.
    /// </summary>
    /// <exception cref="InvalidOperationException">Ghostscript could not read it, e.g. because of a wrong password, or found no Pages in it.</exception>
    public static async Task<byte[]> RewriteAsync(byte[] content, string? password = null)
    {
        var workFolder = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "OfficeBuddy", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            const string original = "Original.pdf";
            const string rewritten = "Neu.pdf";
            await File.WriteAllBytesAsync(Path.Combine(workFolder, original), content);
            var pages = 0;
            await RunAsync(workFolder,
            [
                "-dBATCH", "-dNOPAUSE", "-dSAFER",
                "-sDEVICE=pdfwrite",
                "-sColorConversionStrategy=LeaveColorUnchanged",
                "-dAutoRotatePages=/None",
                .. password is null ? Array.Empty<string>() : [$"-sPDFPassword={password}"],
                $"-sOutputFile={rewritten}",
                original,
            ], pageStarted: _ => Interlocked.Increment(ref pages));
            if (pages == 0)
            {
                // Ghostscript still writes a blank Page when it found none.
                throw new InvalidOperationException("Ghostscript found no Pages to write.");
            }
            return await File.ReadAllBytesAsync(Path.Combine(workFolder, rewritten));
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static async Task RunAsync(string workFolder, IEnumerable<string> arguments, Action<int>? pageStarted)
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
        foreach (var argument in arguments)
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

    private static IEnumerable<string> DownsampleArguments(string inputFile, string outputFile, int dpi) =>
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
