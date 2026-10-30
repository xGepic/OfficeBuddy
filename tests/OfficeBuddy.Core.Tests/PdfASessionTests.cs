using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Tests.Support;
using OfficeBuddy.Core.Tools;

namespace OfficeBuddy.Core.Tests;

public sealed class PdfASessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task A_Document_is_saved_as_PDF_A_2b_with_its_Pages_unchanged()
    {
        var application = SamplePdfs.Create(_folder.Path, "Antrag.pdf", "A1", "A2", "A3");
        var session = new PdfASession();

        (await session.OpenAsync(application)).ShouldBeNull();
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        session.SuggestedFileName.ShouldBe("Antrag (PDF-A).pdf");
        session.HasUnsavedChanges.ShouldBeFalse();
        var saved = SavedPdfA.Read(output);
        saved.Part.ShouldBe("2");
        saved.Conformance.ShouldBe("B");
        saved.OutputIntent.ShouldBe("GTS_PDFA1");
        saved.HasOutputProfile.ShouldBeTrue();
        SavedDocument.ReadLabels(output).ShouldBe(["A1", "A2", "A3"]);
    }

    [Fact]
    public async Task Turned_Pages_look_the_same()
    {
        var session = new PdfASession();
        await session.OpenAsync(SamplePdfs.CreateWithTopBar(_folder.Path, "Plan.pdf", rotation: 90));
        var plan = _folder.File("Plan (PDF-A).pdf");
        await session.SaveAsync(plan);

        var page = await SavedDocument.RenderAsync(plan, 0);
        page.IsLandscape.ShouldBeTrue();
        (page.ColourAt((0.98, 0.5)).R < 50).ShouldBeTrue("Expected the bar on the right");
        (page.ColourAt((0.02, 0.5)).R > 200).ShouldBeTrue("Expected no bar on the left");

        // Sideways text on an upright Page: Ghostscript would turn such a Page by itself if allowed.
        await session.OpenAsync(SamplePdfs.Create(_folder.Path, "Scan.pdf", new SamplePage("S1", Sideways: true)));
        var scan = _folder.File("Scan (PDF-A).pdf");
        await session.SaveAsync(scan);

        (await SavedDocument.RenderAsync(scan, 0)).IsLandscape.ShouldBeFalse();
    }

    [Fact]
    public void Nothing_can_be_saved_before_a_Document_is_open()
    {
        var session = new PdfASession();

        session.CanSave.ShouldBeFalse();
        session.SuggestedFileName.ShouldBeNull();
    }

    [Fact]
    public async Task A_file_that_is_not_a_PDF_or_needs_a_password_is_refused()
    {
        var photo = SamplePdfs.CreateNotAPdf(_folder.Path, "Foto.jpg");
        var statement = SamplePdfs.CreateProtected(_folder.Path, "Kontoauszug.pdf");
        var session = new PdfASession();

        (await session.OpenAsync(photo))!.Reason.ShouldBe(LoadFailureReason.Unsupported);
        (await session.OpenAsync(statement))!.Reason.ShouldBe(LoadFailureReason.Protected);
        session.CanSave.ShouldBeFalse();
    }
}
