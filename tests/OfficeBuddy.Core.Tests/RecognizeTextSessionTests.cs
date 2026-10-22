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

        session.Text.ShouldContain("Rechnung", Case.Sensitive);
        session.Text.ShouldContain("4711", Case.Sensitive);
        (session.WordCount >= 8).ShouldBeTrue($"Expected at least 8 words, found {session.WordCount}: {session.Text}");
    }

    [SkippableFact]
    public async Task The_saved_Document_can_be_searched_and_looks_the_same()
    {
        var session = await OpenScanAsync();
        var scan = session.Document!.FilePath;
        SavedDocument.ReadLabels(scan).ShouldBe([""]);

        await session.RecognizeAsync();
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        session.SuggestedFileName.ShouldBe("Brief (durchsuchbar).pdf");
        var text = SavedDocument.ReadLabels(output).Single();
        text.ShouldContain("Rechnung", Case.Sensitive);
        text.ShouldContain("4711", Case.Sensitive);

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
        (changed < before.Pixels.Length / 1000).ShouldBeTrue($"{changed} of {before.Pixels.Length} colour values changed");
    }

    [SkippableFact]
    public async Task Recognising_can_be_undone_and_saving_needs_recognised_words()
    {
        var session = await OpenScanAsync();
        session.CanSave.ShouldBeFalse();

        var progress = new List<RecognizeProgress>();
        await session.RecognizeAsync(new SynchronousProgress<RecognizeProgress>(progress.Add));
        session.CanSave.ShouldBeTrue();
        progress[^1].ShouldBe(new RecognizeProgress(1, 1));

        session.Undo();
        session.Pages.ShouldBeNull();
        session.CanSave.ShouldBeFalse();
    }

    private async Task<RecognizeTextSession> OpenScanAsync()
    {
        Skip.IfNot(RecognizeTextSession.IsAvailable, "Windows has no text recognition installed here.");
        var scan = await SamplePdfs.CreateTextScanAsync(_folder.Path, "Brief.pdf",
            "Rechnung Nummer 4711 vom 3. Oktober",
            "Bitte überweisen Sie den Betrag bis Ende des Monats.");
        var session = new RecognizeTextSession();
        (await session.OpenAsync(scan)).ShouldBeNull();
        return session;
    }
}
