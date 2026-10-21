using PdfManager.Core.Tests.Support;

namespace PdfManager.Core.Tests;

public sealed class ExtractPagesSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Extracting_a_single_Page_saves_just_that_Page()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var session = new ExtractPagesSession();
        await session.OpenAsync(invoice);

        session.SetSelection([1]);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(["S2"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task Extracted_Pages_keep_their_original_order_and_rotation()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf",
            new SamplePage("S1"), new SamplePage("S2", Rotation: 90), new SamplePage("S3"),
            new SamplePage("S4"), new SamplePage("S5", Rotation: 180));
        var session = new ExtractPagesSession();
        await session.OpenAsync(scan);

        session.SetSelection([4, 1, 2]);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(
            [new SavedPage("S2", 90), new SavedPage("S3", 0), new SavedPage("S5", 180)],
            SavedDocument.ReadPages(output));
    }

    [Fact]
    public async Task Saving_needs_at_least_one_selected_Page()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2");
        var session = new ExtractPagesSession();
        Assert.False(session.CanSave);

        await session.OpenAsync(invoice);
        Assert.False(session.CanSave);

        session.SetSelection([0]);
        Assert.True(session.CanSave);

        session.SetSelection([]);
        Assert.False(session.CanSave);
    }

    [Theory]
    [InlineData(new[] { 1 }, "Rechnung (Seite 2).pdf")]
    [InlineData(new[] { 1, 2 }, "Rechnung (Seiten 2-3).pdf")]
    [InlineData(new[] { 0, 2 }, "Rechnung (Seiten 1, 3).pdf")]
    [InlineData(new[] { 5, 0, 3, 4 }, "Rechnung (Seiten 1, 4-6).pdf")]
    public async Task The_suggested_name_lists_the_selected_Pages_compactly(int[] selection, string expected)
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3", "S4", "S5", "S6");
        var session = new ExtractPagesSession();
        await session.OpenAsync(invoice);

        session.SetSelection(selection);

        Assert.Equal(expected, session.SuggestedFileName);
        Assert.Equal(_folder.Path, session.SuggestedFolder);
    }

    [Fact]
    public async Task Selection_changes_can_be_undone_and_redone()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var session = new ExtractPagesSession();
        await session.OpenAsync(invoice);
        session.SetSelection([0]);
        session.SetSelection([0, 2]);

        session.Undo();
        Assert.Equal([0], session.SelectedPageIndexes);

        session.Redo();
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);
        Assert.Equal(["S1", "S3"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task Selecting_what_is_already_selected_is_not_a_change()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2");
        var session = new ExtractPagesSession();
        await session.OpenAsync(invoice);
        session.SetSelection([1]);

        session.SetSelection([1]);
        session.Undo();

        Assert.Empty(session.SelectedPageIndexes);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public async Task The_source_Document_is_left_unchanged()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var before = await File.ReadAllBytesAsync(invoice);
        var session = new ExtractPagesSession();
        await session.OpenAsync(invoice);

        session.SetSelection([0, 2]);
        await session.SaveAsync(_folder.File("Rechnung (Seiten 1, 3).pdf"));

        Assert.Equal(before, await File.ReadAllBytesAsync(invoice));
    }

    [Fact]
    public async Task Blank_pages_are_not_looked_for_here()
    {
        var scan = SamplePdfs.CreateDuplexScan(_folder.Path, "Scan.pdf");
        var session = new ExtractPagesSession();
        await session.OpenAsync(scan);
        Assert.Empty(session.SelectedPageIndexes);

        session.SetSelection(Enumerable.Range(0, 9));
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(["S1", "R1", "S2", "U1", "S3", "R2", "BEZAHLT", "R3", "N1"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task A_file_that_cannot_be_opened_is_reported_and_the_open_Document_stays()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2");
        var photo = SamplePdfs.CreateNotAPdf(_folder.Path, "Foto.jpg");
        var session = new ExtractPagesSession();
        await session.OpenAsync(invoice);
        session.SetSelection([1]);

        var failure = await session.OpenAsync(photo);

        Assert.Equal(new LoadFailure(photo, LoadFailureReason.Unsupported), failure);
        Assert.Equal("Rechnung.pdf", session.Document?.FileName);
        Assert.Equal([1], session.SelectedPageIndexes);
    }
}
