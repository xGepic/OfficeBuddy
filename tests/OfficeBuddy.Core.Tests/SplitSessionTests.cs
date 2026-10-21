using OfficeBuddy.Core.Tests.Support;

namespace OfficeBuddy.Core.Tests;

public sealed class SplitSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Split_points_cut_the_Document_into_Parts_saved_as_numbered_files()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3", "S4", "S5", "S6");
        var session = new SplitSession();
        await session.OpenAsync(invoice);

        session.ToggleSplitPoint(2);
        session.ToggleSplitPoint(4);
        var output = Directory.CreateDirectory(_folder.File("Teile")).FullName;
        var written = await session.SaveAsync(output, "Rechnung");

        Assert.Equal(
            [
                Path.Combine(output, "Rechnung - Teil 1.pdf"),
                Path.Combine(output, "Rechnung - Teil 2.pdf"),
                Path.Combine(output, "Rechnung - Teil 3.pdf"),
            ],
            written);
        Assert.Equal(["S1", "S2"], SavedDocument.ReadLabels(written[0]));
        Assert.Equal(["S3", "S4"], SavedDocument.ReadLabels(written[1]));
        Assert.Equal(["S5", "S6"], SavedDocument.ReadLabels(written[2]));
    }

    [Fact]
    public async Task Split_points_can_sit_at_either_end_and_a_second_click_removes_one()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3", "S4");
        var session = new SplitSession();
        await session.OpenAsync(invoice);
        Assert.Equal([new PageRange(0, 3)], session.Parts);

        session.ToggleSplitPoint(1);
        session.ToggleSplitPoint(3);
        Assert.Equal([new PageRange(0, 0), new PageRange(1, 2), new PageRange(3, 3)], session.Parts);

        session.ToggleSplitPoint(3);
        Assert.Equal([new PageRange(0, 0), new PageRange(1, 3)], session.Parts);
    }

    [Fact]
    public async Task Each_Page_separately_makes_every_Page_its_own_Part_and_keeps_the_Split_points_for_later()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var session = new SplitSession();
        await session.OpenAsync(invoice);
        session.ToggleSplitPoint(2);

        session.SetEachPageSeparately(true);
        Assert.Equal([new PageRange(0, 0), new PageRange(1, 1), new PageRange(2, 2)], session.Parts);
        var written = await session.SaveAsync(_folder.Path, "Rechnung");
        Assert.Equal([["S1"], ["S2"], ["S3"]], written.Select(SavedDocument.ReadLabels));

        session.SetEachPageSeparately(false);
        Assert.Equal([new PageRange(0, 1), new PageRange(2, 2)], session.Parts);
    }

    [Fact]
    public async Task With_ten_or_more_Parts_the_numbers_are_zero_padded_so_the_files_sort_in_order()
    {
        var labels = Enumerable.Range(1, 12).Select(n => $"S{n}").ToArray();
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", labels);
        var session = new SplitSession();
        await session.OpenAsync(scan);

        session.SetEachPageSeparately(true);
        var written = await session.SaveAsync(_folder.Path, "Scan");

        Assert.Equal("Scan - Teil 01.pdf", Path.GetFileName(written[0]));
        Assert.Equal("Scan - Teil 12.pdf", Path.GetFileName(written[11]));
        Assert.Equal(written, written.Order(StringComparer.Ordinal));
        Assert.Equal(["S10"], SavedDocument.ReadLabels(written[9]));
    }

    [Fact]
    public async Task Saving_needs_at_least_two_Parts()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2");
        var note = SamplePdfs.Create(_folder.Path, "Notiz.pdf", "N1");
        var session = new SplitSession();
        Assert.False(session.CanSave);

        await session.OpenAsync(invoice);
        Assert.False(session.CanSave);

        session.ToggleSplitPoint(1);
        Assert.True(session.CanSave);

        await session.OpenAsync(note);
        session.SetEachPageSeparately(true);
        Assert.False(session.CanSave);
    }

    [Fact]
    public async Task Split_points_and_each_Page_separately_can_be_undone_and_redone()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var session = new SplitSession();
        await session.OpenAsync(invoice);
        session.ToggleSplitPoint(1);
        session.SetEachPageSeparately(true);

        session.Undo();
        Assert.Equal([new PageRange(0, 0), new PageRange(1, 2)], session.Parts);

        session.Undo();
        Assert.Equal([new PageRange(0, 2)], session.Parts);

        session.Redo();
        session.Redo();
        Assert.Equal(3, session.Parts.Count);
    }

    [Fact]
    public async Task Every_Part_keeps_its_Pages_in_order_with_their_rotation()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf",
            new SamplePage("S1"), new SamplePage("S2", Rotation: 90), new SamplePage("S3", Rotation: 270), new SamplePage("S4"));
        var session = new SplitSession();
        await session.OpenAsync(scan);

        session.ToggleSplitPoint(2);
        var written = await session.SaveAsync(_folder.Path, "Scan");

        Assert.Equal([new SavedPage("S1", 0), new SavedPage("S2", 90)], SavedDocument.ReadPages(written[0]));
        Assert.Equal([new SavedPage("S3", 270), new SavedPage("S4", 0)], SavedDocument.ReadPages(written[1]));
    }

    [Fact]
    public async Task The_suggested_base_name_and_folder_come_from_the_Document()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2");
        var session = new SplitSession();
        Assert.Null(session.SuggestedBaseName);

        await session.OpenAsync(invoice);

        Assert.Equal("Rechnung", session.SuggestedBaseName);
        Assert.Equal(_folder.Path, session.SuggestedFolder);
    }

    [Fact]
    public async Task The_file_names_a_save_would_write_are_known_beforehand()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var session = new SplitSession();
        await session.OpenAsync(invoice);
        session.ToggleSplitPoint(1);

        Assert.Equal(["Brief - Teil 1.pdf", "Brief - Teil 2.pdf"], session.FileNamesFor("Brief"));
    }

    [Fact]
    public async Task If_one_Part_cannot_be_written_nothing_is_left_half_saved()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var output = Directory.CreateDirectory(_folder.File("Teile")).FullName;
        var earlierPart1 = SamplePdfs.Create(output, "Rechnung - Teil 1.pdf", "ALT");
        Directory.CreateDirectory(Path.Combine(output, "Rechnung - Teil 2.pdf"));
        var session = new SplitSession();
        await session.OpenAsync(invoice);
        session.ToggleSplitPoint(1);

        await Assert.ThrowsAnyAsync<IOException>(() => session.SaveAsync(output, "Rechnung"));

        Assert.Equal(["ALT"], SavedDocument.ReadLabels(earlierPart1));
        Assert.Equal(["Rechnung - Teil 1.pdf", "Rechnung - Teil 2.pdf"],
            Directory.EnumerateFileSystemEntries(output).Select(Path.GetFileName).Order());
        Assert.True(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task Saving_clears_the_undo_history()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2");
        var session = new SplitSession();
        await session.OpenAsync(invoice);
        session.ToggleSplitPoint(1);

        await session.SaveAsync(_folder.Path, "Rechnung");

        Assert.False(session.CanUndo);
        Assert.False(session.HasUnsavedChanges);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Split_points_can_only_sit_between_Pages(int pageIndex)
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var session = new SplitSession();
        await session.OpenAsync(invoice);

        Assert.Throws<ArgumentOutOfRangeException>(() => session.ToggleSplitPoint(pageIndex));
        Assert.False(session.CanUndo);
    }
}
