using OfficeBuddy.Core.Tests.Support;
using OfficeBuddy.Core.Tools;

namespace OfficeBuddy.Core.Tests;

public sealed class PageToImageSessionTests : IDisposable
{
    // An A4 Page (210 × 297 mm) at 200 dpi.
    private const double A4WidthAt200Dpi = 210 / 25.4 * 200;
    private const double A4HeightAt200Dpi = 297 / 25.4 * 200;

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Selected_Pages_are_saved_as_JPG_images_at_200_dpi_named_after_their_page_numbers()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3", "S4");
        var session = new PageToImageSession();
        await session.OpenAsync(invoice);

        session.SetSelection([1, 3]);
        var output = Directory.CreateDirectory(_folder.File("Bilder")).FullName;
        var written = await session.SaveAsync(output, "Rechnung");

        written.ShouldBe([Path.Combine(output, "Rechnung - Seite 2.jpg"), Path.Combine(output, "Rechnung - Seite 4.jpg")]);
        Directory.GetFiles(output).Select(Path.GetFileName).Order().ShouldBe(["Rechnung - Seite 2.jpg", "Rechnung - Seite 4.jpg"]);
        foreach (var path in written)
        {
            var image = await SavedImage.ReadAsync(path);
            image.IsJpeg.ShouldBeTrue();
            ((double)image.Width).ShouldBe(A4WidthAt200Dpi, tolerance: 1);
            ((double)image.Height).ShouldBe(A4HeightAt200Dpi, tolerance: 1);
        }
    }

    [Fact]
    public async Task A_rotated_Page_becomes_a_rotated_image()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", new SamplePage("S1"), new SamplePage("S2", Rotation: 90));
        var session = new PageToImageSession();
        await session.OpenAsync(scan);

        session.SetSelection([1]);
        var written = await session.SaveAsync(_folder.Path, "Scan");

        var image = await SavedImage.ReadAsync(written.ShouldHaveSingleItem());
        ((double)image.Width).ShouldBe(A4HeightAt200Dpi, tolerance: 1);
        ((double)image.Height).ShouldBe(A4WidthAt200Dpi, tolerance: 1);
    }

    [Fact]
    public async Task Saving_needs_a_selected_Page_and_selecting_can_be_undone()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2");
        var session = new PageToImageSession();
        await session.OpenAsync(invoice);
        session.CanSave.ShouldBeFalse();

        session.SetSelection([0]);
        session.CanSave.ShouldBeTrue();

        session.Undo();
        session.CanSave.ShouldBeFalse();
        session.SelectedPageIndexes.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_suggested_base_name_and_the_file_names_are_known_before_saving()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var session = new PageToImageSession();
        await session.OpenAsync(invoice);

        session.SetSelection([2, 0]);

        session.SuggestedBaseName.ShouldBe("Rechnung");
        session.SuggestedFolder.ShouldBe(_folder.Path);
        session.FileNamesFor("Brief").ShouldBe(["Brief - Seite 1.jpg", "Brief - Seite 3.jpg"]);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(270)]
    public async Task The_image_shows_the_Page_turned_the_way_it_is_rotated(int rotation)
    {
        var page = SamplePdfs.CreateWithTopBar(_folder.Path, "Plan.pdf", rotation);
        var session = new PageToImageSession();
        await session.OpenAsync(page);

        session.SetSelection([0]);
        var image = await SavedImage.ReadAsync((await session.SaveAsync(_folder.Path, "Plan")).ShouldHaveSingleItem());

        // Rotated clockwise by 90°, the top edge ends up on the right; by 270°, on the left.
        var (barSide, otherSide) = rotation == 90
            ? (image.Darkness.Right, image.Darkness.Left)
            : (image.Darkness.Left, image.Darkness.Right);
        (barSide > 200).ShouldBeTrue($"Expected the bar on that side, darkness {barSide}");
        (otherSide < 20).ShouldBeTrue($"Expected no bar on the other side, darkness {otherSide}");
    }

    [Fact]
    public async Task A_huge_Page_still_becomes_an_image_of_a_size_that_fits_in_memory()
    {
        var plan = SamplePdfs.CreateHuge(_folder.Path, "Plan.pdf");
        var session = new PageToImageSession();
        await session.OpenAsync(plan);

        session.SetSelection([0]);
        var image = await SavedImage.ReadAsync((await session.SaveAsync(_folder.Path, "Plan")).ShouldHaveSingleItem());

        // At 200 dpi this Page would be almost 40000 pixels across; it is scaled down to at most 64 megapixels.
        image.IsJpeg.ShouldBeTrue();
        ((long)image.Width * image.Height).ShouldBeInRange(60_000_000, 64_000_000);
    }

    [Fact]
    public async Task Saving_tells_how_many_images_are_written_so_far()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1", "S2", "S3");
        var session = new PageToImageSession();
        await session.OpenAsync(invoice);
        session.SetSelection([0, 2]);
        var reported = new List<int>();

        await session.SaveAsync(_folder.Path, "Rechnung", new SynchronousProgress<int>(reported.Add));

        reported.ShouldBe([1, 2]);
    }
}
