using OfficeBuddy.Core.Tests.Support;
using static OfficeBuddy.Core.Tests.Support.SampleImages;

namespace OfficeBuddy.Core.Tests;

public sealed class ResizeImageSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task A_photo_scaled_to_50_percent_is_saved_as_a_JPG_half_as_wide_and_high()
    {
        var photo = await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue);
        var session = new ResizeImageSession();
        await session.OpenAsync(photo);

        session.SetPercentage(50);
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Equal("Urlaub (200x150).jpg", session.SuggestedFileName);
        var image = await SavedImage.ReadAsync(output);
        Assert.True(image.IsJpeg);
        Assert.Equal((200, 150), (image.Width, image.Height));
        Assert.True(image.Darkness.Top > 200, $"Expected the bar still on top, darkness {image.Darkness.Top}");
    }

    [Fact]
    public async Task A_PNG_stays_a_PNG_and_can_also_be_made_bigger()
    {
        var drawing = await CreatePngAsync(_folder.Path, "Plan.png", 300, 200, Green);
        var session = new ResizeImageSession();
        await session.OpenAsync(drawing);

        session.SetPercentage(150);
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Equal("Plan (450x300).png", session.SuggestedFileName);
        var image = await SavedImage.ReadAsync(output);
        Assert.False(image.IsJpeg);
        Assert.Equal((450, 300), (image.Width, image.Height));
    }

    [Fact]
    public async Task With_proportions_kept_the_other_side_follows_and_the_percentage_is_known()
    {
        var photo = await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue);
        var session = new ResizeImageSession();
        await session.OpenAsync(photo);

        session.SetWidth(100, keepProportions: true);
        Assert.Equal(new ImageSize(100, 75), session.Size);
        Assert.Equal(25, session.Percentage);

        session.SetHeight(150, keepProportions: true);
        Assert.Equal(new ImageSize(200, 150), session.Size);
        Assert.Equal(50, session.Percentage);
    }

    [Fact]
    public async Task Without_proportions_width_and_height_are_set_freely_and_saved_exactly()
    {
        var photo = await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue);
        var session = new ResizeImageSession();
        await session.OpenAsync(photo);

        session.SetWidth(320, keepProportions: false);
        session.SetHeight(100, keepProportions: false);
        Assert.Equal(new ImageSize(320, 100), session.Size);
        Assert.Null(session.Percentage);

        var output = _folder.File("Banner.jpg");
        await session.SaveAsync(output);
        var image = await SavedImage.ReadAsync(output);
        Assert.Equal((320, 100), (image.Width, image.Height));
    }

    [Fact]
    public async Task A_sideways_phone_photo_is_sized_and_saved_the_right_way_up()
    {
        // Stored landscape with the bar on top; the phone notes it must be turned right to be upright.
        var photo = await CreateJpegAsync(_folder.Path, "IMG_0815.jpg", 400, 300, Red, exifOrientation: 6);
        var session = new ResizeImageSession();
        await session.OpenAsync(photo);
        Assert.Equal(new ImageSize(300, 400), session.OriginalSize);

        session.SetWidth(150, keepProportions: true);
        var output = _folder.File("IMG_0815 klein.jpg");
        await session.SaveAsync(output);

        // Written upright, without an orientation note: the bar is on the right in the pixels themselves.
        var image = await SavedImage.ReadAsync(output);
        Assert.Equal((150, 200), (image.Width, image.Height));
        Assert.True(image.Darkness.Right > 200, $"Expected the bar on the right, darkness {image.Darkness.Right}");
        Assert.True(image.Darkness.Left < 200, $"Expected the photo itself on the left, darkness {image.Darkness.Left}");
        Assert.True(image.Darkness.Top < 200, $"Expected no bar on top, darkness {image.Darkness.Top}");
    }

    [Fact]
    public async Task Every_new_size_can_be_undone_and_saving_needs_a_size_that_differs_from_the_original()
    {
        var photo = await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue);
        var session = new ResizeImageSession();
        Assert.False(session.CanSave);
        await session.OpenAsync(photo);
        Assert.False(session.CanSave);
        Assert.Equal(100, session.Percentage);

        session.SetPercentage(50);
        session.SetPercentage(25);
        Assert.True(session.CanSave);
        Assert.True(session.HasUnsavedChanges);

        session.Undo();
        Assert.Equal(new ImageSize(200, 150), session.Size);
        session.Undo();
        Assert.Equal(new ImageSize(400, 300), session.Size);
        Assert.False(session.CanSave);
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task Setting_the_size_it_already_has_is_not_a_change()
    {
        var photo = await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue);
        var session = new ResizeImageSession();
        await session.OpenAsync(photo);

        session.SetWidth(400, keepProportions: true);

        Assert.False(session.CanUndo);
    }

    [Fact]
    public async Task A_size_too_big_to_fit_in_memory_is_scaled_down_in_proportion()
    {
        var photo = await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue);
        var session = new ResizeImageSession();
        await session.OpenAsync(photo);

        session.SetPercentage(10_000);

        Assert.InRange(session.Size!.Pixels, 63_000_000, 64_000_000);
        Assert.InRange(session.Percentage!.Value, 2300, 2310);
    }

    [Fact]
    public async Task A_HEIC_photo_is_rejected_as_unsupported()
    {
        var iphone = CreateHeic(_folder.Path, "IMG_4711.HEIC");
        var session = new ResizeImageSession();

        var failure = await session.OpenAsync(iphone);

        Assert.Equal(new LoadFailure(iphone, LoadFailureReason.Unsupported), failure);
        Assert.Null(session.Image);
    }
}
