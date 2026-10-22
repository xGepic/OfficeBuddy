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

        session.SuggestedFileName.ShouldBe("Brief (zugeschnitten).jpg");
        session.ResultSize.ShouldBe(new ImageSize(200, 150));
        var image = await SavedImage.ReadAsync(output);
        image.IsJpeg.ShouldBeTrue();
        (image.Width, image.Height).ShouldBe((200, 150));
        // Both bars are cut away.
        (image.Darkness.Top < 200).ShouldBeTrue($"Expected no bar on top, darkness {image.Darkness.Top}");
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
        (image.Width, image.Height).ShouldBe((200, 150));
        (image.Darkness.Top > 200).ShouldBeTrue($"Expected the bar on top, darkness {image.Darkness.Top}");
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

        session.TurnedSize.ShouldBe(new ImageSize(300, 400));
        var image = await SavedImage.ReadAsync(output);
        (image.Width, image.Height).ShouldBe((300, 400));
        (image.Darkness.Right > 200).ShouldBeTrue($"Expected the bar on the right, darkness {image.Darkness.Right}");
        (image.Darkness.Top < 200).ShouldBeTrue($"Expected no bar on top, darkness {image.Darkness.Top}");
    }

    [Fact]
    public async Task The_kept_part_turns_with_the_image()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue));
        session.SetArea(new CropArea(0, 0, 0.5, 0.25));

        // The top left corner ends up top right, and back again.
        session.RotateRight();
        session.Area.ShouldBe(new CropArea(0.75, 0, 0.25, 0.5));
        session.ResultSize.ShouldBe(new ImageSize(75, 200));
        session.RotateLeft();
        session.Area.ShouldBe(new CropArea(0, 0, 0.5, 0.25));

        session.RotateLeft();
        session.Rotation.ShouldBe(270);
        session.Area.ShouldBe(new CropArea(0, 0.5, 0.25, 0.5));
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
        (image.Width, image.Height).ShouldBe((300, 200));
        (image.Darkness.Right > 200).ShouldBeTrue($"Expected the bar on the right, darkness {image.Darkness.Right}");
        (image.Darkness.Left < 200).ShouldBeTrue($"Expected the photo itself on the left, darkness {image.Darkness.Left}");
    }

    [Fact]
    public async Task The_picture_on_screen_shows_the_image_the_right_way_up_and_turned()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "IMG_0815.jpg", 400, 300, Red, exifOrientation: 6));
        var image = session.Image!;

        PngSize.Read(await image.RenderPngAsync(200, 200)).ShouldBe((150, 200));
        PngSize.Read(await image.RenderPngAsync(200, 200, rotation: 90)).ShouldBe((200, 150));
        PngSize.Read(await image.RenderPngAsync(1000, 1000, rotation: 180)).ShouldBe((300, 400));
    }

    [Fact]
    public async Task A_PNG_stays_a_PNG()
    {
        var session = await OpenAsync(await CreatePngAsync(_folder.Path, "Plan.png", 300, 200, Green));

        session.SetArea(new CropArea(0.1, 0.1, 0.5, 0.5));
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        session.SuggestedFileName.ShouldBe("Plan (zugeschnitten).png");
        var image = await SavedImage.ReadAsync(output);
        image.IsJpeg.ShouldBeFalse();
        (image.Width, image.Height).ShouldBe((150, 100));
    }

    [Fact]
    public async Task The_kept_part_stays_on_the_image_and_a_few_pixels_big()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue));

        session.SetArea(new CropArea(-0.2, 0.5, 0.5, 0.9));
        Rounded(session.Area).ShouldBe(new CropArea(0, 0.1, 0.5, 0.9));

        session.SetArea(new CropArea(0.5, 0.5, 0, 0));
        session.ResultSize.ShouldBe(new ImageSize(8, 8));
    }

    [Fact]
    public async Task Every_change_can_be_undone_and_saving_needs_a_cut_or_a_turn()
    {
        var session = new CropImageSession();
        session.CanSave.ShouldBeFalse();
        await session.OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue));
        session.CanSave.ShouldBeFalse();

        session.SetArea(new CropArea(0.1, 0.1, 0.5, 0.5));
        session.RotateRight();
        session.KeepWhole();
        session.CanSave.ShouldBeTrue();

        session.Undo();
        session.Rotation.ShouldBe(90);
        session.Undo();
        session.Rotation.ShouldBe(0);
        session.Undo();
        session.Area.ShouldBe(CropArea.Whole);
        session.CanSave.ShouldBeFalse();
        session.HasUnsavedChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task Keeping_the_area_it_already_has_is_not_a_change()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue));

        session.KeepWhole();

        session.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public async Task A_HEIC_photo_is_rejected_as_unsupported()
    {
        var iphone = CreateHeic(_folder.Path, "IMG_4711.HEIC");
        var session = new CropImageSession();

        var failure = await session.OpenAsync(iphone);

        failure.ShouldBe(new LoadFailure(iphone, LoadFailureReason.Unsupported));
        session.Image.ShouldBeNull();
    }

    private static async Task<CropImageSession> OpenAsync(string image)
    {
        var session = new CropImageSession();
        (await session.OpenAsync(image)).ShouldBeNull();
        return session;
    }

    private static CropArea Rounded(CropArea area) =>
        new(Math.Round(area.Left, 6), Math.Round(area.Top, 6), Math.Round(area.Width, 6), Math.Round(area.Height, 6));

    private static async Task AssertColourAsync(string path, double x, double y, (int R, int G, int B) expected)
    {
        var actual = await SavedImage.ColourAtAsync(path, x, y);
        SavedImage.AreAlike(actual, expected).ShouldBeTrue($"Expected {expected} at ({x}, {y}), found {actual}");
    }
}
