using OfficeBuddy.Core.Tests.Support;
using static OfficeBuddy.Core.Tests.Support.SampleImages;

namespace OfficeBuddy.Core.Tests;

public sealed class ScanSessionTests : IDisposable
{
    // A4 in points, upright.
    private const double A4Width = 595.3;
    private const double A4Height = 841.9;

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Scanned_Pages_are_saved_in_order_each_the_size_of_the_scanned_paper()
    {
        // An A4 letter and a 10 × 15 cm receipt, both scanned at 200 dpi.
        var letter = await CreateJpegAsync(_folder.Path, "Seite1.jpg", 1654, 2339, Blue, dpi: 200);
        var receipt = await CreateJpegAsync(_folder.Path, "Seite2.jpg", 787, 1181, Green, dpi: 200);
        var session = new ScanSession();

        await session.AddAsync([letter, receipt]);
        var output = _folder.File("Scan.pdf");
        await session.SaveAsync(output);

        var pages = SavedDocument.ReadPageSizes(output);
        Assert.Equal(2, pages.Count);
        AssertSize(pages[0], A4Width, A4Height);
        AssertSize(pages[1], 283.3, 425.2);
    }

    [Fact]
    public async Task A_Page_scanned_sideways_can_be_turned()
    {
        var letter = await CreateJpegAsync(_folder.Path, "Seite1.jpg", 1654, 2339, Blue, dpi: 200);
        var session = new ScanSession();
        await session.AddAsync([letter]);

        session.RotateRight(0);
        var output = _folder.File("Scan.pdf");
        await session.SaveAsync(output);

        Assert.True(session.Pages[0].IsLandscape);
        AssertSize(SavedDocument.ReadPageSizes(output)[0], A4Height, A4Width);
        // The black bar along the top of the picture is now on the right.
        var page = await SavedDocument.RenderAsync(output, 0);
        Assert.True(page.ColourAt((0.97, 0.5)).R < 60, $"Expected the bar on the right, found {page.ColourAt((0.97, 0.5))}");
        Assert.True(page.ColourAt((0.5, 0.03)).B > 150, $"Expected the picture's blue on top, found {page.ColourAt((0.5, 0.03))}");
    }

    [Fact]
    public async Task A_picture_without_a_believable_resolution_gets_a_Page_as_long_as_A4()
    {
        // A phone photo states 72 dpi, which would make a Page over a metre high.
        var photo = await CreateJpegAsync(_folder.Path, "IMG_0815.jpg", 3000, 4000, Blue, dpi: 72);
        var session = new ScanSession();
        await session.AddAsync([photo]);

        var output = _folder.File("Scan.pdf");
        await session.SaveAsync(output);

        AssertSize(SavedDocument.ReadPageSizes(output)[0], A4Height * 3 / 4, A4Height);
    }

    [Fact]
    public async Task Reordering_turning_and_removing_can_be_undone()
    {
        var first = await CreateJpegAsync(_folder.Path, "Seite1.jpg", 200, 280, Blue, dpi: 200);
        var second = await CreateJpegAsync(_folder.Path, "Seite2.jpg", 200, 280, Green, dpi: 200);
        var session = new ScanSession();
        Assert.False(session.CanSave);
        await session.AddAsync([first]);
        await session.AddAsync([second]);

        session.Move(1, 0);
        session.RotateLeft(0);
        session.Remove(1);
        Assert.Equal(["Seite2.jpg"], session.Pages.Select(page => page.Image.FileName));
        Assert.Equal(270, session.Pages[0].Rotation);

        session.Undo();
        session.Undo();
        Assert.Equal(0, session.Pages[0].Rotation);
        session.Undo();
        Assert.Equal(["Seite1.jpg", "Seite2.jpg"], session.Pages.Select(page => page.Image.FileName));
    }

    [Fact]
    public async Task The_suggested_name_tells_when_it_was_scanned_and_it_goes_to_Documents()
    {
        var session = new ScanSession();
        await session.AddAsync([await CreateJpegAsync(_folder.Path, "Seite1.jpg", 200, 280, Blue, dpi: 200)]);

        Assert.Matches(@"^Scan \d{4}-\d{2}-\d{2} \d{2}\.\d{2}\.pdf$", session.SuggestedFileName);
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), session.SuggestedFolder);
    }

    [Fact]
    public async Task The_scanned_files_can_be_deleted_once_added()
    {
        var scanned = await CreateJpegAsync(_folder.Path, "Seite1.jpg", 200, 280, Blue, dpi: 200);
        var session = new ScanSession();
        await session.AddAsync([scanned]);

        File.Delete(scanned);
        var output = _folder.File("Scan.pdf");
        await session.SaveAsync(output);

        Assert.Single(SavedDocument.ReadPageSizes(output));
    }

    private static void AssertSize((double Width, double Height) actual, double width, double height)
    {
        Assert.InRange(actual.Width, width - 1.5, width + 1.5);
        Assert.InRange(actual.Height, height - 1.5, height + 1.5);
    }
}
