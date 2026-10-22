using OfficeBuddy.Core.Tests.Support;

namespace OfficeBuddy.Core.Tests;

/// <summary>Skipped where Windows has no text recognition installed, e.g. on some build machines.</summary>
public sealed class RecognizeTextSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [SkippableFact]
    public async Task The_text_of_a_scan_is_recognised()
    {
        var session = await OpenScanAsync();

        await session.RecognizeAsync();

        Assert.Contains("Rechnung", session.Text);
        Assert.Contains("4711", session.Text);
        Assert.True(session.WordCount >= 8, $"Expected at least 8 words, found {session.WordCount}: {session.Text}");
    }

    [SkippableFact]
    public async Task The_saved_Document_can_be_searched_and_looks_the_same()
    {
        var session = await OpenScanAsync();
        var scan = session.Document!.FilePath;
        Assert.Equal([""], SavedDocument.ReadLabels(scan));

        await session.RecognizeAsync();
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Equal("Brief (durchsuchbar).pdf", session.SuggestedFileName);
        var text = SavedDocument.ReadLabels(output).Single();
        Assert.Contains("Rechnung", text);
        Assert.Contains("4711", text);

        // The text is invisible: the Page looks as it did.
        var before = await SavedDocument.RenderAsync(scan, 0);
        var after = await SavedDocument.RenderAsync(output, 0);
        var changed = 0;
        for (var i = 0; i < before.Pixels.Length; i++)
        {
            if (Math.Abs(before.Pixels[i] - after.Pixels[i]) > 64)
            {
                changed++;
            }
        }
        Assert.True(changed < before.Pixels.Length / 1000, $"{changed} of {before.Pixels.Length} colour values changed");
    }

    [SkippableFact]
    public async Task Recognising_can_be_undone_and_saving_needs_recognised_words()
    {
        var session = await OpenScanAsync();
        Assert.False(session.CanSave);

        var progress = new List<RecognizeProgress>();
        await session.RecognizeAsync(new SynchronousProgress<RecognizeProgress>(progress.Add));
        Assert.True(session.CanSave);
        Assert.Equal(new RecognizeProgress(1, 1), progress[^1]);

        session.Undo();
        Assert.Null(session.Pages);
        Assert.False(session.CanSave);
    }

    private async Task<RecognizeTextSession> OpenScanAsync()
    {
        Skip.IfNot(RecognizeTextSession.IsAvailable, "Windows has no text recognition installed here.");
        var scan = await SamplePdfs.CreateTextScanAsync(_folder.Path, "Brief.pdf",
            "Rechnung Nummer 4711 vom 3. Oktober",
            "Bitte überweisen Sie den Betrag bis Ende des Monats.");
        var session = new RecognizeTextSession();
        Assert.Null(await session.OpenAsync(scan));
        return session;
    }
}
