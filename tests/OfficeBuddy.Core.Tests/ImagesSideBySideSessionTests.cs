using OfficeBuddy.Core.Tests.Support;
using static OfficeBuddy.Core.Tests.Support.SampleImages;

namespace OfficeBuddy.Core.Tests;

public sealed class ImagesSideBySideSessionTests : IDisposable
{
    private static readonly (int R, int G, int B) White = (255, 255, 255);

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Two_photos_are_saved_side_by_side_at_the_height_of_the_lower_one()
    {
        var front = await CreateJpegAsync(_folder.Path, "Ausweis vorne.jpg", 400, 300, Blue);
        var back = await CreateJpegAsync(_folder.Path, "Ausweis hinten.jpg", 200, 150, Red);
        var session = new ImagesSideBySideSession();

        await session.AddAsync([front, back]);
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        // Both 200 × 150, with a gap of 2 % of the height between them.
        Assert.Equal("Ausweis vorne (nebeneinander).jpg", session.SuggestedFileName);
        Assert.Equal(new ImageSize(403, 150), session.ResultSize);
        var image = await SavedImage.ReadAsync(output);
        Assert.True(image.IsJpeg);
        Assert.Equal((403, 150), (image.Width, image.Height));
        await AssertColourAsync(output, 0.25, 0.6, Blue);
        await AssertColourAsync(output, 0.75, 0.6, Red);
    }

    [Fact]
    public async Task One_below_the_other_they_share_the_width_of_the_narrower_one()
    {
        var front = await CreateJpegAsync(_folder.Path, "Ausweis vorne.jpg", 400, 300, Blue);
        var back = await CreateJpegAsync(_folder.Path, "Ausweis hinten.jpg", 200, 150, Red);
        var session = new ImagesSideBySideSession();
        await session.AddAsync([front, back]);

        session.Arrange(ImageArrangement.OneBelowTheOther);
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Equal("Ausweis vorne (untereinander).jpg", session.SuggestedFileName);
        var image = await SavedImage.ReadAsync(output);
        Assert.Equal((200, 304), (image.Width, image.Height));
        await AssertColourAsync(output, 0.5, 0.35, Blue);
        await AssertColourAsync(output, 0.5, 0.8, Red);
    }

    [Fact]
    public async Task Only_PNGs_give_a_PNG_with_a_white_gap()
    {
        var plan = await CreatePngAsync(_folder.Path, "Plan.png", 300, 200, Green);
        var sketch = await CreatePngAsync(_folder.Path, "Skizze.png", 300, 200, Blue);
        var session = new ImagesSideBySideSession();
        await session.AddAsync([plan, sketch]);

        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Equal("Plan (nebeneinander).png", session.SuggestedFileName);
        var image = await SavedImage.ReadAsync(output);
        Assert.False(image.IsJpeg);
        Assert.Equal((604, 200), (image.Width, image.Height));
        await AssertColourAsync(output, 0.5, 0.6, White);
    }

    [Fact]
    public async Task A_photo_among_PNGs_gives_a_JPG()
    {
        var plan = await CreatePngAsync(_folder.Path, "Plan.png", 300, 200, Green);
        var photo = await CreateJpegAsync(_folder.Path, "Foto.jpg", 300, 200, Blue);
        var session = new ImagesSideBySideSession();

        await session.AddAsync([plan, photo]);

        Assert.False(session.SavesAsPng);
        Assert.Equal("Plan (nebeneinander).jpg", session.SuggestedFileName);
    }

    [Fact]
    public async Task Sideways_phone_photos_are_lined_up_the_right_way_up()
    {
        // Stored landscape; the phone notes it must be turned right to be upright, so 300 × 400.
        var photo = await CreateJpegAsync(_folder.Path, "IMG_0815.jpg", 400, 300, Red, exifOrientation: 6);
        var other = await CreateJpegAsync(_folder.Path, "IMG_0816.jpg", 300, 400, Blue);
        var session = new ImagesSideBySideSession();

        await session.AddAsync([photo, other]);

        Assert.Equal(new ImageSize(608, 400), session.ResultSize);
    }

    [Fact]
    public async Task Reordering_removing_and_rearranging_can_all_be_undone()
    {
        var front = await CreateJpegAsync(_folder.Path, "Ausweis vorne.jpg", 400, 300, Blue);
        var back = await CreateJpegAsync(_folder.Path, "Ausweis hinten.jpg", 200, 150, Red);
        var session = new ImagesSideBySideSession();
        Assert.False(session.CanSave);
        await session.AddAsync([front]);
        Assert.False(session.CanSave);
        await session.AddAsync([back]);
        Assert.True(session.CanSave);

        session.Move(1, 0);
        Assert.Equal("Ausweis hinten.jpg", session.Images[0].FileName);
        session.Arrange(ImageArrangement.OneBelowTheOther);
        session.Remove(0);
        Assert.False(session.CanSave);

        session.Undo();
        Assert.Equal(2, session.Images.Count);
        session.Undo();
        Assert.Equal(ImageArrangement.SideBySide, session.Arrangement);
        session.Undo();
        Assert.Equal("Ausweis vorne.jpg", session.Images[0].FileName);
    }

    [Fact]
    public async Task Choosing_the_arrangement_it_already_has_is_not_a_change()
    {
        var session = new ImagesSideBySideSession();
        await session.AddAsync([await CreateJpegAsync(_folder.Path, "Foto.jpg", 400, 300, Blue)]);
        session.Undo();

        session.Arrange(ImageArrangement.SideBySide);

        Assert.False(session.CanUndo);
    }

    [Fact]
    public async Task The_preview_fits_the_box_it_is_shown_in()
    {
        var front = await CreateJpegAsync(_folder.Path, "Ausweis vorne.jpg", 400, 300, Blue);
        var back = await CreateJpegAsync(_folder.Path, "Ausweis hinten.jpg", 200, 150, Red);
        var session = new ImagesSideBySideSession();
        await session.AddAsync([front, back]);

        var (width, height) = PngSize.Read(await session.RenderPreviewPngAsync(100, 100));

        Assert.InRange(width, 95, 100);
        Assert.InRange(height, 30, 40);
    }

    [Fact]
    public async Task A_result_too_big_to_fit_in_memory_is_scaled_down()
    {
        var strip = await CreateJpegAsync(_folder.Path, "Streifen.jpg", 100, 20_000, Blue);
        var session = new ImagesSideBySideSession();

        await session.AddAsync(Enumerable.Repeat(strip, 40));

        Assert.InRange(session.ResultSize!.Pixels, 60_000_000, 64_000_000);
    }

    [Fact]
    public async Task A_HEIC_photo_is_left_out_and_the_others_are_added()
    {
        var iphone = CreateHeic(_folder.Path, "IMG_4711.HEIC");
        var photo = await CreateJpegAsync(_folder.Path, "Foto.jpg", 400, 300, Blue);
        var session = new ImagesSideBySideSession();

        var failures = await session.AddAsync([iphone, photo]);

        Assert.Equal([new LoadFailure(iphone, LoadFailureReason.Unsupported)], failures);
        Assert.Single(session.Images);
    }

    private static async Task AssertColourAsync(string path, double x, double y, (int R, int G, int B) expected)
    {
        var actual = await SavedImage.ColourAtAsync(path, x, y);
        Assert.True(SavedImage.AreAlike(actual, expected), $"Expected {expected} at ({x}, {y}), found {actual}");
    }
}
