using PdfManager.Core.Tests.Support;
using static PdfManager.Core.Tests.Support.SampleImages;

namespace PdfManager.Core.Tests;

public sealed class ImagesToPdfSessionTests : IDisposable
{
    // A4 in points, upright.
    private const double A4Width = 595.3;
    private const double A4Height = 841.9;

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Each_image_becomes_one_A4_Page_turned_to_suit_it()
    {
        var letter = await CreatePngAsync(_folder.Path, "Brief.png", 300, 400, Green);
        var receipt = await CreateJpegAsync(_folder.Path, "Quittung.jpg", 400, 300, Blue);
        var session = new ImagesToPdfSession();

        await session.AddAsync([letter, receipt]);
        var output = _folder.File("Brief.pdf");
        await session.SaveAsync(output);

        var pages = SavedDocument.ReadPageSizes(output);
        Assert.Equal(2, pages.Count);
        AssertSize(pages[0], A4Width, A4Height);
        AssertSize(pages[1], A4Height, A4Width);
    }

    [Fact]
    public async Task A_phone_photo_taken_sideways_comes_out_the_right_way_up()
    {
        // Stored landscape with the bar on top; the phone notes it must be turned right to be upright.
        var photo = await CreateJpegAsync(_folder.Path, "IMG_0815.jpg", 400, 300, Red, exifOrientation: 6);
        var session = new ImagesToPdfSession();

        await session.AddAsync([photo]);
        var output = _folder.File("IMG_0815.pdf");
        await session.SaveAsync(output);

        AssertSize(Assert.Single(SavedDocument.ReadPageSizes(output)), A4Width, A4Height);
        var page = await SavedDocument.RenderAsync(output, 0);
        Assert.True(IsDark(page.ColourAt((0.9, 0.5))), "Expected the bar on the right");
        Assert.True(IsColour(page.ColourAt((0.3, 0.5)), Red), "Expected the photo itself on the left");
    }

    [Theory]
    [InlineData(1, "top", "left")]
    [InlineData(2, "top", "right")]
    [InlineData(3, "bottom", "right")]
    [InlineData(4, "bottom", "left")]
    [InlineData(5, "left", "top")]
    [InlineData(6, "right", "top")]
    [InlineData(7, "right", "bottom")]
    [InlineData(8, "left", "bottom")]
    public async Task Every_way_a_camera_notes_its_orientation_is_turned_the_right_way_up(
        ushort exifOrientation, string blackBarSide, string magentaBarSide)
    {
        // Square, so the Page stays upright: 190 × 190 mm, from 10 to 200 mm across and 53.5 to 243.5 mm down.
        var photo = await CreateJpegAsync(_folder.Path, "IMG.jpg", 300, 300, Green, exifOrientation, markLeft: true);
        var session = new ImagesToPdfSession();

        await session.AddAsync([photo]);
        var output = _folder.File("IMG.pdf");
        await session.SaveAsync(output);

        var page = await SavedDocument.RenderAsync(output, 0);
        (double X, double Y) InsideEdge(string side) => side switch
        {
            "top" => (0.5, 61.5 / 297),
            "bottom" => (0.5, 235.5 / 297),
            "left" => (18.0 / 210, 0.5),
            _ => (192.0 / 210, 0.5),
        };
        Assert.True(IsDark(page.ColourAt(InsideEdge(blackBarSide))), $"Expected the black bar at the {blackBarSide}");
        Assert.True(IsColour(page.ColourAt(InsideEdge(magentaBarSide)), Magenta), $"Expected the magenta bar at the {magentaBarSide}");
    }

    [Fact]
    public async Task A_sideways_phone_photo_shows_as_an_upright_thumbnail()
    {
        var photo = await CreateJpegAsync(_folder.Path, "IMG_0815.jpg", 400, 300, Red, exifOrientation: 6);
        var session = new ImagesToPdfSession();
        await session.AddAsync([photo]);

        var thumbnail = await session.Images[0].RenderPngAsync(100, 100);

        Assert.Equal((75, 100), PngSize.Read(thumbnail));
    }

    [Fact]
    public async Task An_image_is_centred_inside_a_small_margin_without_being_stretched()
    {
        // 4:3 on a landscape A4 Page (297 × 210 mm, 10 mm margin): 190 mm high, so 253 mm wide, from 22 mm to 275 mm.
        var receipt = await CreateJpegAsync(_folder.Path, "Quittung.jpg", 400, 300, Blue);
        var session = new ImagesToPdfSession();

        await session.AddAsync([receipt]);
        var output = _folder.File("Quittung.pdf");
        await session.SaveAsync(output);

        var page = await SavedDocument.RenderAsync(output, 0);
        Assert.True(IsWhite(page.ColourAt((0.02, 0.02))), "Expected a white margin in the corner");
        Assert.True(IsWhite(page.ColourAt((17.0 / 297, 0.5))), "Expected white left of the image");
        Assert.True(IsColour(page.ColourAt((27.0 / 297, 0.5)), Blue), "Expected the image from 22 mm on");
        Assert.True(IsColour(page.ColourAt((270.0 / 297, 0.5)), Blue), "Expected the image up to 275 mm");
        Assert.True(IsWhite(page.ColourAt((280.0 / 297, 0.5))), "Expected white right of the image");
        Assert.True(IsWhite(page.ColourAt((0.5, 5.0 / 210))), "Expected the top margin");
    }

    [Fact]
    public async Task Images_keep_the_order_they_were_added_in_until_moved_or_removed_and_that_can_be_undone()
    {
        var red = await CreateJpegAsync(_folder.Path, "1.jpg", 300, 400, Red);
        var green = await CreatePngAsync(_folder.Path, "2.png", 300, 400, Green);
        var blue = await CreateJpegAsync(_folder.Path, "3.jpg", 300, 400, Blue);
        var session = new ImagesToPdfSession();
        await session.AddAsync([red, green, blue]);

        session.Move(2, 0);
        Assert.Equal(["3.jpg", "1.jpg", "2.png"], session.Images.Select(image => image.FileName));
        session.Remove(1);
        Assert.Equal(["3.jpg", "2.png"], session.Images.Select(image => image.FileName));
        session.Undo();
        session.Undo();
        session.Move(0, 1);

        var output = _folder.File("Bilder.pdf");
        await session.SaveAsync(output);
        Assert.Equal([Green, Red, Blue], await CentreColoursAsync(output, 3));
        Assert.False(session.CanUndo);
    }

    [Fact]
    public async Task A_HEIC_photo_is_rejected_as_unsupported_and_the_other_images_are_still_added()
    {
        var letter = await CreatePngAsync(_folder.Path, "Brief.png", 300, 400, Green);
        var iphone = CreateHeic(_folder.Path, "IMG_4711.HEIC");
        var receipt = await CreateJpegAsync(_folder.Path, "Quittung.jpg", 400, 300, Blue);
        var session = new ImagesToPdfSession();

        var failed = await session.AddAsync([letter, iphone, receipt]);

        Assert.Equal([new LoadFailure(iphone, LoadFailureReason.Unsupported)], failed);
        Assert.Equal(["Brief.png", "Quittung.jpg"], session.Images.Select(image => image.FileName));
    }

    [Fact]
    public async Task Saving_needs_an_image_and_suggests_the_first_images_name()
    {
        var session = new ImagesToPdfSession();
        Assert.False(session.CanSave);
        Assert.Null(session.SuggestedFileName);

        await session.AddAsync([await CreateJpegAsync(_folder.Path, "IMG_0815.jpg", 300, 400, Red)]);

        Assert.True(session.CanSave);
        Assert.Equal("IMG_0815.pdf", session.SuggestedFileName);
        Assert.Equal(_folder.Path, session.SuggestedFolder);
    }

    private static void AssertSize((double Width, double Height) page, double width, double height)
    {
        Assert.InRange(page.Width, width - 1, width + 1);
        Assert.InRange(page.Height, height - 1, height + 1);
    }

    private static async Task<IReadOnlyList<(int R, int G, int B)>> CentreColoursAsync(string path, int pageCount)
    {
        var colours = new List<(int R, int G, int B)>();
        for (var i = 0; i < pageCount; i++)
        {
            var centre = (await SavedDocument.RenderAsync(path, i)).ColourAt((0.5, 0.6));
            colours.Add(new[] { Red, Green, Blue }.Single(colour => IsColour(centre, colour)));
        }
        return colours;
    }

    private static bool IsColour((int R, int G, int B) actual, (int R, int G, int B) expected) =>
        Math.Abs(actual.R - expected.R) < 30 && Math.Abs(actual.G - expected.G) < 30 && Math.Abs(actual.B - expected.B) < 30;

    private static bool IsWhite((int R, int G, int B) colour) => colour is { R: > 240, G: > 240, B: > 240 };

    private static bool IsDark((int R, int G, int B) colour) => colour is { R: < 40, G: < 40, B: < 40 };
}
