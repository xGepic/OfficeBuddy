using OfficeBuddy.Core.Tests.Support;

namespace OfficeBuddy.Core.Tests;

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

        session.IsAlreadySmallEnough.ShouldBeTrue();
        session.OriginalSize.ShouldBe(new FileInfo(invoice).Length);

        await session.CompressAsync();
        var output = _folder.File("Rechnung (verkleinert).pdf");
        await session.SaveAsync(output);

        SavedDocument.ReadLabels(output).ShouldBe(["S1", "S2", "S3"]);
        session.Result!.CompressedSize.ShouldBe(new FileInfo(output).Length);
        session.Result.ReachedTarget.ShouldBeTrue();
    }

    [Fact]
    public async Task A_large_colour_scan_ends_up_under_10_MB_in_colour_and_still_readable()
    {
        var scan = await SamplePdfs.CreateColourScanAsync(_folder.Path, "Kontoauszug.pdf", pageCount: 9);
        var session = new CompressSession();
        await session.OpenAsync(scan);
        session.IsAlreadySmallEnough.ShouldBeFalse();

        await session.CompressAsync();
        var output = _folder.File("Kontoauszug (verkleinert).pdf");
        await session.SaveAsync(output);

        session.Result!.ReachedTarget.ShouldBeTrue();
        new FileInfo(output).Length.ShouldBeInRange(1, session.EmailSizeTarget - 1);
        SavedDocument.ReadPages(output).Count.ShouldBe(9);
        SavedDocument.ReadImageResolutions(output).ShouldAllBe(dpi => dpi >= 96);
        var (r, g, b) = (await SavedDocument.RenderAsync(output, pageIndex: 4)).ColourAt(SamplePdfs.ColourScanLogo);
        (r > 150 && g < 90 && b < 90).ShouldBeTrue($"Expected the red logo, got ({r}, {g}, {b})");
    }

    [Fact]
    public async Task Compressing_tells_which_Page_it_is_working_on()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var session = new CompressSession();
        await session.OpenAsync(invoice);
        var reported = new List<CompressProgress>();

        await session.CompressAsync(new SynchronousProgress<CompressProgress>(reported.Add));

        reported.ShouldBe([new(1, 1, 3), new(1, 2, 3), new(1, 3, 3)]);
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
        page.IsLandscape.ShouldBeTrue();
        (page.ColourAt((0.98, 0.5)).R < 50).ShouldBeTrue("Expected the bar on the right");
        (page.ColourAt((0.02, 0.5)).R > 200).ShouldBeTrue("Expected no bar on the left");

        // Sideways text on an upright Page: Ghostscript would turn such a Page by itself if allowed.
        await session.OpenAsync(SamplePdfs.Create(_folder.Path, "Scan.pdf", new SamplePage("S1", Sideways: true)));
        await session.CompressAsync();
        var scan = _folder.File("Scan (verkleinert).pdf");
        await session.SaveAsync(scan);

        (await SavedDocument.RenderAsync(scan, 0)).IsLandscape.ShouldBeFalse();
    }

    [Fact]
    public async Task Compressing_can_be_undone_and_redone_until_saved()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2");
        var session = new CompressSession();
        await session.OpenAsync(invoice);
        session.SuggestedFileName.ShouldBe("Rechnung (verkleinert).pdf");
        session.SuggestedFolder.ShouldBe(_folder.Path);
        session.CanCompress.ShouldBeTrue();
        session.CanSave.ShouldBeFalse();

        await session.CompressAsync();
        session.CanCompress.ShouldBeFalse();
        session.CanSave.ShouldBeTrue();
        session.HasUnsavedChanges.ShouldBeTrue();

        session.Undo();
        session.Result.ShouldBeNull();
        session.CanCompress.ShouldBeTrue();
        session.CanSave.ShouldBeFalse();
        session.HasUnsavedChanges.ShouldBeFalse();

        session.Redo();
        await session.SaveAsync(_folder.File("Rechnung (verkleinert).pdf"));
        session.CanUndo.ShouldBeFalse();
        session.HasUnsavedChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task A_scan_that_cannot_get_under_10_MB_is_reported_and_kept_as_small_as_it_got()
    {
        var scan = await SamplePdfs.CreateDetailedLowResolutionScanAsync(_folder.Path, "Fotos.pdf", pageCount: 12);
        var session = new CompressSession();
        await session.OpenAsync(scan);
        var reported = new List<CompressProgress>();

        await session.CompressAsync(new SynchronousProgress<CompressProgress>(reported.Add));

        // All three attempts were made, and the last one is kept.
        session.Result!.ReachedTarget.ShouldBeFalse();
        reported.ShouldContain(progress => progress.Step == 3);
        (session.Result.CompressedSize >= session.EmailSizeTarget).ShouldBeTrue();
    }

    [Fact]
    public async Task With_a_20_MB_email_limit_a_scan_that_cannot_get_under_10_MB_is_already_small_enough()
    {
        var scan = await SamplePdfs.CreateDetailedLowResolutionScanAsync(_folder.Path, "Fotos.pdf", pageCount: 12);
        var session = new CompressSession(emailSizeLimitMegabytes: 20);
        await session.OpenAsync(scan);
        (session.OriginalSize > 10L * 1024 * 1024).ShouldBeTrue();
        session.IsAlreadySmallEnough.ShouldBeTrue();

        await session.CompressAsync();

        session.Result!.ReachedTarget.ShouldBeTrue();
        session.CanSaveAsParts.ShouldBeFalse();
    }

    [Fact]
    public async Task A_scan_under_10_MB_is_only_compressed_lightly()
    {
        var scan = await SamplePdfs.CreateColourScanAsync(_folder.Path, "Brief.pdf", pageCount: 2);
        var session = new CompressSession();
        await session.OpenAsync(scan);
        session.IsAlreadySmallEnough.ShouldBeTrue();
        var reported = new List<CompressProgress>();

        await session.CompressAsync(new SynchronousProgress<CompressProgress>(reported.Add));
        var output = _folder.File("Brief (verkleinert).pdf");
        await session.SaveAsync(output);

        reported.ShouldAllBe(progress => progress.Step == 1);
        SavedDocument.ReadImageResolutions(output).ShouldAllBe(dpi => dpi >= 145 && dpi <= 155);
        session.Result!.IsSmaller.ShouldBeTrue();
    }

    [Fact]
    public async Task A_scan_that_cannot_get_under_10_MB_can_be_saved_as_Parts_that_each_fit_an_email()
    {
        var scan = await SamplePdfs.CreateDetailedLowResolutionScanAsync(_folder.Path, "Fotos.pdf", pageCount: 25);
        var session = new CompressSession();
        await session.OpenAsync(scan);

        await session.CompressAsync();
        session.Result!.ReachedTarget.ShouldBeFalse();
        session.CanSaveAsParts.ShouldBeTrue();

        var output = Directory.CreateDirectory(_folder.File("Teile")).FullName;
        var names = session.PartFileNamesFor("Fotos");
        var written = await session.SaveAsPartsAsync(output, "Fotos");

        // About 23 MB in Parts under 10 MB: at least three, numbered in page order.
        names.Count.ShouldBeInRange(3, 9);
        names.ShouldBe(Enumerable.Range(1, names.Count).Select(number => $"Fotos - Teil {number}.pdf"));
        written.ShouldBe(names.Select(name => Path.Combine(output, name)));
        written.ShouldAllBe(path => new FileInfo(path).Length >= 1 && new FileInfo(path).Length < session.EmailSizeTarget);
        written.SelectMany(SavedDocument.ReadLabels).ShouldBe(Enumerable.Range(1, 25).Select(number => $"F{number}"));
        session.HasUnsavedChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task A_Page_too_big_for_an_email_on_its_own_is_named_instead_of_being_split_off()
    {
        var scan = await SamplePdfs.CreateDetailedLowResolutionScanAsync(_folder.Path, "Plan.pdf", pageCount: 3, posterPageNumber: 2);
        var session = new CompressSession();
        await session.OpenAsync(scan);

        await session.CompressAsync();

        session.Result!.ReachedTarget.ShouldBeFalse();
        session.CanSaveAsParts.ShouldBeFalse();
        session.Result.OversizedPageNumbers.ShouldBe([2]);
    }

    [Fact]
    public async Task A_Document_that_reached_the_target_is_not_offered_as_Parts()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2");
        var session = new CompressSession();
        await session.OpenAsync(invoice);

        await session.CompressAsync();

        session.Result!.ReachedTarget.ShouldBeTrue();
        session.CanSaveAsParts.ShouldBeFalse();
        session.Result.OversizedPageNumbers.ShouldBeEmpty();
    }
}
