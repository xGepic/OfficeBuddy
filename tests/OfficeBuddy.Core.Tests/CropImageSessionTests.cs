using OfficeBuddy.Core.Tests.Support;
using static OfficeBuddy.Core.Tests.Support.SampleImages;

namespace OfficeBuddy.Core.Tests;

public sealed class CropImageSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Only_the_kept_part_is_saved_at_its_full_size()
    {
        // Black bar along the top, magenta bar down the left.
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue, markLeft: true));

        session.SetArea(new CropArea(0.5, 0.5, 0.5, 0.5));
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Equal("Brief (zugeschnitten).jpg", session.SuggestedFileName);
        Assert.Equal(new ImageSize(200, 150), session.ResultSize);
        var image = await SavedImage.ReadAsync(output);
        Assert.True(image.IsJpeg);
        Assert.Equal((200, 150), (image.Width, image.Height));
        // Both bars are cut away.
        Assert.True(image.Darkness.Top < 200, $"Expected no bar on top, darkness {image.Darkness.Top}");
        await AssertColourAsync(output, 0.05, 0.5, Blue);
    }

    [Fact]
    public async Task The_top_left_corner_keeps_both_bars()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue, markLeft: true));

        session.SetArea(new CropArea(0, 0, 0.5, 0.5));
        var output = _folder.File("Ecke.jpg");
        await session.SaveAsync(output);

        var image = await SavedImage.ReadAsync(output);
        Assert.Equal((200, 150), (image.Width, image.Height));
        Assert.True(image.Darkness.Top > 200, $"Expected the bar on top, darkness {image.Darkness.Top}");
        await AssertColourAsync(output, 0.1, 0.6, Magenta);
        await AssertColourAsync(output, 0.8, 0.6, Blue);
    }

    [Fact]
    public async Task Turning_right_saves_the_image_turned_with_the_top_on_the_right()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue));

        session.RotateRight();
        var output = _folder.File("Brief gedreht.jpg");
        await session.SaveAsync(output);

        Assert.Equal(new ImageSize(300, 400), session.TurnedSize);
        var image = await SavedImage.ReadAsync(output);
        Assert.Equal((300, 400), (image.Width, image.Height));
        Assert.True(image.Darkness.Right > 200, $"Expected the bar on the right, darkness {image.Darkness.Right}");
        Assert.True(image.Darkness.Top < 200, $"Expected no bar on top, darkness {image.Darkness.Top}");
    }

    [Fact]
    public async Task The_kept_part_turns_with_the_image()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue));
        session.SetArea(new CropArea(0, 0, 0.5, 0.25));

        // The top left corner ends up top right, and back again.
        session.RotateRight();
        Assert.Equal(new CropArea(0.75, 0, 0.25, 0.5), session.Area);
        Assert.Equal(new ImageSize(75, 200), session.ResultSize);
        session.RotateLeft();
        Assert.Equal(new CropArea(0, 0, 0.5, 0.25), session.Area);

        session.RotateLeft();
        Assert.Equal(270, session.Rotation);
        Assert.Equal(new CropArea(0, 0.5, 0.25, 0.5), session.Area);
    }

    [Fact]
    public async Task A_sideways_phone_photo_is_cut_the_right_way_up()
    {
        // Stored landscape with the bar on top; upright it is 300 × 400 with the bar on the right.
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "IMG_0815.jpg", 400, 300, Red, exifOrientation: 6));

        session.SetArea(new CropArea(0, 0, 1, 0.5));
        var output = _folder.File("IMG_0815 oben.jpg");
        await session.SaveAsync(output);

        var image = await SavedImage.ReadAsync(output);
        Assert.Equal((300, 200), (image.Width, image.Height));
        Assert.True(image.Darkness.Right > 200, $"Expected the bar on the right, darkness {image.Darkness.Right}");
        Assert.True(image.Darkness.Left < 200, $"Expected the photo itself on the left, darkness {image.Darkness.Left}");
    }

    [Fact]
    public async Task The_picture_on_screen_shows_the_image_the_right_way_up_and_turned()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "IMG_0815.jpg", 400, 300, Red, exifOrientation: 6));
        var image = session.Image!;

        Assert.Equal((150, 200), PngSize.Read(await image.RenderPngAsync(200, 200)));
        Assert.Equal((200, 150), PngSize.Read(await image.RenderPngAsync(200, 200, rotation: 90)));
        Assert.Equal((300, 400), PngSize.Read(await image.RenderPngAsync(1000, 1000, rotation: 180)));
    }

    [Fact]
    public async Task A_PNG_stays_a_PNG()
    {
        var session = await OpenAsync(await CreatePngAsync(_folder.Path, "Plan.png", 300, 200, Green));

        session.SetArea(new CropArea(0.1, 0.1, 0.5, 0.5));
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Equal("Plan (zugeschnitten).png", session.SuggestedFileName);
        var image = await SavedImage.ReadAsync(output);
        Assert.False(image.IsJpeg);
        Assert.Equal((150, 100), (image.Width, image.Height));
    }

    [Fact]
    public async Task The_kept_part_stays_on_the_image_and_a_few_pixels_big()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue));

        session.SetArea(new CropArea(-0.2, 0.5, 0.5, 0.9));
        Assert.Equal(new CropArea(0, 0.1, 0.5, 0.9), Rounded(session.Area));

        session.SetArea(new CropArea(0.5, 0.5, 0, 0));
        Assert.Equal(new ImageSize(8, 8), session.ResultSize);
    }

    [Fact]
    public async Task Every_change_can_be_undone_and_saving_needs_a_cut_or_a_turn()
    {
        var session = new CropImageSession();
        Assert.False(session.CanSave);
        await session.OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue));
        Assert.False(session.CanSave);

        session.SetArea(new CropArea(0.1, 0.1, 0.5, 0.5));
        session.RotateRight();
        session.KeepWhole();
        Assert.True(session.CanSave);

        session.Undo();
        Assert.Equal(90, session.Rotation);
        session.Undo();
        Assert.Equal(0, session.Rotation);
        session.Undo();
        Assert.Equal(CropArea.Whole, session.Area);
        Assert.False(session.CanSave);
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task Keeping_the_area_it_already_has_is_not_a_change()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue));

        session.KeepWhole();

        Assert.False(session.CanUndo);
    }

    [Fact]
    public async Task A_HEIC_photo_is_rejected_as_unsupported()
    {
        var iphone = CreateHeic(_folder.Path, "IMG_4711.HEIC");
        var session = new CropImageSession();

        var failure = await session.OpenAsync(iphone);

        Assert.Equal(new LoadFailure(iphone, LoadFailureReason.Unsupported), failure);
        Assert.Null(session.Image);
    }

    private static async Task<CropImageSession> OpenAsync(string image)
    {
        var session = new CropImageSession();
        Assert.Null(await session.OpenAsync(image));
        return session;
    }

    private static CropArea Rounded(CropArea area) =>
        new(Math.Round(area.Left, 6), Math.Round(area.Top, 6), Math.Round(area.Width, 6), Math.Round(area.Height, 6));

    private static async Task AssertColourAsync(string path, double x, double y, (int R, int G, int B) expected)
    {
        var actual = await SavedImage.ColourAtAsync(path, x, y);
        Assert.True(SavedImage.AreAlike(actual, expected), $"Expected {expected} at ({x}, {y}), found {actual}");
    }
}
