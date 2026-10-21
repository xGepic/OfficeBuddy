using PdfManager.Core.Tests.Support;

namespace PdfManager.Core.Tests;

public sealed class CompressSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task A_small_digital_invoice_is_already_small_enough_and_keeps_its_text_selectable_after_a_light_compression()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var session = new CompressSession();
        await session.OpenAsync(invoice);

        Assert.True(session.IsAlreadySmallEnough);
        Assert.Equal(new FileInfo(invoice).Length, session.OriginalSize);

        await session.CompressAsync();
        var output = _folder.File("Rechnung (verkleinert).pdf");
        await session.SaveAsync(output);

        Assert.Equal(["S1", "S2", "S3"], SavedDocument.ReadLabels(output));
        Assert.Equal(new FileInfo(output).Length, session.Result!.CompressedSize);
        Assert.True(session.Result.ReachedTarget);
    }

    [Fact]
    public async Task A_large_colour_scan_ends_up_under_10_MB_in_colour_and_still_readable()
    {
        var scan = await SamplePdfs.CreateColourScanAsync(_folder.Path, "Kontoauszug.pdf", pageCount: 9);
        var session = new CompressSession();
        await session.OpenAsync(scan);
        Assert.False(session.IsAlreadySmallEnough);

        await session.CompressAsync();
        var output = _folder.File("Kontoauszug (verkleinert).pdf");
        await session.SaveAsync(output);

        Assert.True(session.Result!.ReachedTarget);
        Assert.InRange(new FileInfo(output).Length, 1, CompressSession.EmailSizeTarget - 1);
        Assert.Equal(9, SavedDocument.ReadPages(output).Count);
        Assert.All(SavedDocument.ReadImageResolutions(output), dpi => Assert.True(dpi >= 96, $"Image at {dpi:0} dpi"));
        var (r, g, b) = (await SavedDocument.RenderAsync(output, pageIndex: 4)).ColourAt(SamplePdfs.ColourScanLogo);
        Assert.True(r > 150 && g < 90 && b < 90, $"Expected the red logo, got ({r}, {g}, {b})");
    }

    [Fact]
    public async Task Compressing_tells_which_Page_it_is_working_on()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var session = new CompressSession();
        await session.OpenAsync(invoice);
        var reported = new List<CompressProgress>();

        await session.CompressAsync(new SynchronousProgress<CompressProgress>(reported.Add));

        Assert.Equal([new(1, 1, 3), new(1, 2, 3), new(1, 3, 3)], reported);
    }

    [Fact]
    public async Task Compressing_leaves_every_Page_turned_the_way_it_was()
    {
        var session = new CompressSession();

        // A Page rotated clockwise: its top edge, with the bar, shows on the right.
        await session.OpenAsync(SamplePdfs.CreateWithTopBar(_folder.Path, "Plan.pdf", rotation: 90));
        await session.CompressAsync();
        var plan = _folder.File("Plan (verkleinert).pdf");
        await session.SaveAsync(plan);

        var page = await SavedDocument.RenderAsync(plan, 0);
        Assert.True(page.IsLandscape);
        Assert.True(page.ColourAt((0.98, 0.5)).R < 50, "Expected the bar on the right");
        Assert.True(page.ColourAt((0.02, 0.5)).R > 200, "Expected no bar on the left");

        // Sideways text on an upright Page: Ghostscript would turn such a Page by itself if allowed.
        await session.OpenAsync(SamplePdfs.Create(_folder.Path, "Scan.pdf", new SamplePage("S1", Sideways: true)));
        await session.CompressAsync();
        var scan = _folder.File("Scan (verkleinert).pdf");
        await session.SaveAsync(scan);

        Assert.False((await SavedDocument.RenderAsync(scan, 0)).IsLandscape);
    }

    [Fact]
    public async Task Compressing_can_be_undone_and_redone_until_saved()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2");
        var session = new CompressSession();
        await session.OpenAsync(invoice);
        Assert.Equal("Rechnung (verkleinert).pdf", session.SuggestedFileName);
        Assert.Equal(_folder.Path, session.SuggestedFolder);
        Assert.True(session.CanCompress);
        Assert.False(session.CanSave);

        await session.CompressAsync();
        Assert.False(session.CanCompress);
        Assert.True(session.CanSave);
        Assert.True(session.HasUnsavedChanges);

        session.Undo();
        Assert.Null(session.Result);
        Assert.True(session.CanCompress);
        Assert.False(session.CanSave);
        Assert.False(session.HasUnsavedChanges);

        session.Redo();
        await session.SaveAsync(_folder.File("Rechnung (verkleinert).pdf"));
        Assert.False(session.CanUndo);
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task A_scan_that_cannot_get_under_10_MB_is_reported_and_kept_as_small_as_it_got()
    {
        var scan = await SamplePdfs.CreateDetailedLowResolutionScanAsync(_folder.Path, "Fotos.pdf", pageCount: 12);
        var session = new CompressSession();
        await session.OpenAsync(scan);
        var reported = new List<CompressProgress>();

        await session.CompressAsync(new SynchronousProgress<CompressProgress>(reported.Add));

        // All three attempts were made, and the last one is kept (ticket 10 splits it into Parts).
        Assert.False(session.Result!.ReachedTarget);
        Assert.Equal(3, reported[^1].Step);
        Assert.True(session.Result.CompressedSize >= CompressSession.EmailSizeTarget);
    }

    [Fact]
    public async Task A_scan_under_10_MB_is_only_compressed_lightly()
    {
        var scan = await SamplePdfs.CreateColourScanAsync(_folder.Path, "Brief.pdf", pageCount: 2);
        var session = new CompressSession();
        await session.OpenAsync(scan);
        Assert.True(session.IsAlreadySmallEnough);
        var reported = new List<CompressProgress>();

        await session.CompressAsync(new SynchronousProgress<CompressProgress>(reported.Add));
        var output = _folder.File("Brief (verkleinert).pdf");
        await session.SaveAsync(output);

        Assert.All(reported, progress => Assert.Equal(1, progress.Step));
        Assert.All(SavedDocument.ReadImageResolutions(output), dpi => Assert.InRange(dpi, 145, 155));
        Assert.True(session.Result!.IsSmaller);
    }
}
