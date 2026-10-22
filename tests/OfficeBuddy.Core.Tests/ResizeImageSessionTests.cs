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

        session.SuggestedFileName.ShouldBe("Urlaub (200x150).jpg");
        var image = await SavedImage.ReadAsync(output);
        image.IsJpeg.ShouldBeTrue();
        (image.Width, image.Height).ShouldBe((200, 150));
        (image.Darkness.Top > 200).ShouldBeTrue($"Expected the bar still on top, darkness {image.Darkness.Top}");
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

        session.SuggestedFileName.ShouldBe("Plan (450x300).png");
        var image = await SavedImage.ReadAsync(output);
        image.IsJpeg.ShouldBeFalse();
        (image.Width, image.Height).ShouldBe((450, 300));
    }

    [Fact]
    public async Task With_proportions_kept_the_other_side_follows_and_the_percentage_is_known()
    {
        var photo = await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue);
        var session = new ResizeImageSession();
        await session.OpenAsync(photo);

        session.SetWidth(100, keepProportions: true);
        session.Size.ShouldBe(new ImageSize(100, 75));
        session.Percentage.ShouldBe(25);

        session.SetHeight(150, keepProportions: true);
        session.Size.ShouldBe(new ImageSize(200, 150));
        session.Percentage.ShouldBe(50);
    }

    [Fact]
    public async Task Without_proportions_width_and_height_are_set_freely_and_saved_exactly()
    {
        var photo = await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue);
        var session = new ResizeImageSession();
        await session.OpenAsync(photo);

        session.SetWidth(320, keepProportions: false);
        session.SetHeight(100, keepProportions: false);
        session.Size.ShouldBe(new ImageSize(320, 100));
        session.Percentage.ShouldBeNull();

        var output = _folder.File("Banner.jpg");
        await session.SaveAsync(output);
        var image = await SavedImage.ReadAsync(output);
        (image.Width, image.Height).ShouldBe((320, 100));
    }

    [Fact]
    public async Task A_sideways_phone_photo_is_sized_and_saved_the_right_way_up()
    {
        // Stored landscape with the bar on top; the phone notes it must be turned right to be upright.
        var photo = await CreateJpegAsync(_folder.Path, "IMG_0815.jpg", 400, 300, Red, exifOrientation: 6);
        var session = new ResizeImageSession();
        await session.OpenAsync(photo);
        session.OriginalSize.ShouldBe(new ImageSize(300, 400));

        session.SetWidth(150, keepProportions: true);
        var output = _folder.File("IMG_0815 klein.jpg");
        await session.SaveAsync(output);

        // Written upright, without an orientation note: the bar is on the right in the pixels themselves.
        var image = await SavedImage.ReadAsync(output);
        (image.Width, image.Height).ShouldBe((150, 200));
        (image.Darkness.Right > 200).ShouldBeTrue($"Expected the bar on the right, darkness {image.Darkness.Right}");
        (image.Darkness.Left < 200).ShouldBeTrue($"Expected the photo itself on the left, darkness {image.Darkness.Left}");
        (image.Darkness.Top < 200).ShouldBeTrue($"Expected no bar on top, darkness {image.Darkness.Top}");
    }

    [Fact]
    public async Task Every_new_size_can_be_undone_and_saving_needs_a_size_that_differs_from_the_original()
    {
        var photo = await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue);
        var session = new ResizeImageSession();
        session.CanSave.ShouldBeFalse();
        await session.OpenAsync(photo);
        session.CanSave.ShouldBeFalse();
        session.Percentage.ShouldBe(100);

        session.SetPercentage(50);
        session.SetPercentage(25);
        session.CanSave.ShouldBeTrue();
        session.HasUnsavedChanges.ShouldBeTrue();

        session.Undo();
        session.Size.ShouldBe(new ImageSize(200, 150));
        session.Undo();
        session.Size.ShouldBe(new ImageSize(400, 300));
        session.CanSave.ShouldBeFalse();
        session.HasUnsavedChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task Setting_the_size_it_already_has_is_not_a_change()
    {
        var photo = await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue);
        var session = new ResizeImageSession();
        await session.OpenAsync(photo);

        session.SetWidth(400, keepProportions: true);

        session.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public async Task A_size_too_big_to_fit_in_memory_is_scaled_down_in_proportion()
    {
        var photo = await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue);
        var session = new ResizeImageSession();
        await session.OpenAsync(photo);

        session.SetPercentage(10_000);

        session.Size!.Pixels.ShouldBeInRange(63_000_000, 64_000_000);
        session.Percentage!.Value.ShouldBeInRange(2300, 2310);
    }

    [Fact]
    public async Task A_HEIC_photo_is_rejected_as_unsupported()
    {
        var iphone = CreateHeic(_folder.Path, "IMG_4711.HEIC");
        var session = new ResizeImageSession();

        var failure = await session.OpenAsync(iphone);

        failure.ShouldBe(new LoadFailure(iphone, LoadFailureReason.Unsupported));
        session.Image.ShouldBeNull();
    }
}
