using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Images;
using OfficeBuddy.Core.Tests.Support;
using OfficeBuddy.Core.Tools;
using static OfficeBuddy.Core.Tests.Support.SampleImages;

namespace OfficeBuddy.Core.Tests;

public sealed class PictureInPictureSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task An_inserted_image_starts_in_the_middle_at_most_a_third_as_wide_and_high()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue));

        await session.InsertAsync(await CreatePngAsync(_folder.Path, "Logo.png", 100, 100, Red));

        // Square on a 4:3 image: a quarter of the width is a third of the height.
        var placement = session.Inserted!.Placement;
        placement.Width.ShouldBe(0.25, tolerance: 1e-6);
        session.HeightFor(placement.Width).ShouldBe(1.0 / 3, tolerance: 1e-6);
        placement.Left.ShouldBe(0.375, tolerance: 1e-6);
        placement.Top.ShouldBe(1.0 / 3, tolerance: 1e-6);
    }

    [Fact]
    public async Task The_inserted_image_is_saved_where_it_was_placed_on_the_photo_at_the_photos_size()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue));
        await session.InsertAsync(await CreatePngAsync(_folder.Path, "Logo.png", 100, 100, Red));

        session.Place(new Placement(0.5, 0.5, 0.25));
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        session.SuggestedFileName.ShouldBe("Urlaub (Bild in Bild).jpg");
        var image = await SavedImage.ReadAsync(output);
        image.IsJpeg.ShouldBeTrue();
        (image.Width, image.Height).ShouldBe((400, 300));
        await AssertColourAsync(output, 0.62, 0.72, Red);
        await AssertColourAsync(output, 0.62, 0.3, Blue);
        await AssertColourAsync(output, 0.2, 0.72, Blue);
    }

    [Fact]
    public async Task Where_a_signature_is_transparent_the_photo_shows_through()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Brief.jpg", 400, 300, Blue));
        await session.InsertAsync(await CreateSignatureAsync(_folder.Path, "Unterschrift.png", 200, 60));

        // Half as wide as the letter, so a fifth as high: the stroke runs from 0.567 to 0.633 down.
        session.Place(new Placement(0, 0.5, 0.5));
        var output = _folder.File("Brief unterschrieben.jpg");
        await session.SaveAsync(output);

        await AssertColourAsync(output, 0.25, 0.6, (0, 0, 0));
        await AssertColourAsync(output, 0.25, 0.52, Blue);
        await AssertColourAsync(output, 0.25, 0.68, Blue);
    }

    [Fact]
    public async Task The_inserted_image_always_stays_whole_on_the_photo()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue));
        await session.InsertAsync(await CreatePngAsync(_folder.Path, "Logo.png", 100, 100, Red));

        session.Place(new Placement(0.9, 0.9, 0.25));
        var placement = session.Inserted!.Placement;
        placement.Left.ShouldBe(0.75, tolerance: 1e-6);
        placement.Top.ShouldBe(2.0 / 3, tolerance: 1e-6);

        // As big as it gets: as high as the photo.
        session.Place(new Placement(0, 0, 2));
        placement = session.Inserted!.Placement;
        placement.Width.ShouldBe(0.75, tolerance: 1e-6);
        session.HeightFor(placement.Width).ShouldBe(1, tolerance: 1e-6);
    }

    [Fact]
    public async Task A_PNG_stays_a_PNG()
    {
        var session = await OpenAsync(await CreatePngAsync(_folder.Path, "Plan.png", 300, 200, Green));
        await session.InsertAsync(await CreateJpegAsync(_folder.Path, "Foto.jpg", 100, 100, Red));

        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        session.SuggestedFileName.ShouldBe("Plan (Bild in Bild).png");
        var image = await SavedImage.ReadAsync(output);
        image.IsJpeg.ShouldBeFalse();
        (image.Width, image.Height).ShouldBe((300, 200));
    }

    [Fact]
    public async Task A_sideways_phone_photo_is_saved_the_right_way_up()
    {
        // Stored landscape with the bar on top; the phone notes it must be turned right to be upright.
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "IMG_0815.jpg", 400, 300, Red, exifOrientation: 6));
        await session.InsertAsync(await CreatePngAsync(_folder.Path, "Logo.png", 100, 100, Green));

        var output = _folder.File("IMG_0815 mit Logo.jpg");
        await session.SaveAsync(output);

        var image = await SavedImage.ReadAsync(output);
        (image.Width, image.Height).ShouldBe((300, 400));
        (image.Darkness.Right > 200).ShouldBeTrue($"Expected the bar on the right, darkness {image.Darkness.Right}");
    }

    [Fact]
    public async Task Inserting_moving_and_removing_can_all_be_undone()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue));
        session.CanSave.ShouldBeFalse();

        await session.InsertAsync(await CreatePngAsync(_folder.Path, "Logo.png", 100, 100, Red));
        var start = session.Inserted!.Placement;
        session.Place(new Placement(0, 0, 0.1));
        session.Remove();
        session.Inserted.ShouldBeNull();
        session.CanSave.ShouldBeFalse();

        session.Undo();
        session.Inserted!.Placement.ShouldBe(new Placement(0, 0, 0.1));
        session.Undo();
        session.Inserted!.Placement.ShouldBe(start);
        session.CanSave.ShouldBeTrue();
        session.Undo();
        session.Inserted.ShouldBeNull();
        session.HasUnsavedChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task Placing_it_where_it_already_is_is_not_a_change()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue));
        await session.InsertAsync(await CreatePngAsync(_folder.Path, "Logo.png", 100, 100, Red));
        session.Place(new Placement(0.75, 0.5, 0.25));

        // Already as far right as it goes, so one undo goes back to where it was inserted.
        session.Place(new Placement(0.9, 0.5, 0.25));
        session.Undo();

        session.Inserted!.Placement.Left.ShouldBe(0.375, tolerance: 1e-6);
    }

    [Fact]
    public async Task Opening_another_photo_starts_over_without_an_inserted_image()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue));
        await session.InsertAsync(await CreatePngAsync(_folder.Path, "Logo.png", 100, 100, Red));

        await session.OpenAsync(await CreateJpegAsync(_folder.Path, "Garten.jpg", 300, 300, Green));

        session.MainImage!.FileName.ShouldBe("Garten.jpg");
        session.Inserted.ShouldBeNull();
        session.CanUndo.ShouldBeFalse();
        session.HasUnsavedChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task A_HEIC_photo_is_rejected_as_unsupported_and_changes_nothing()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue));
        var iphone = CreateHeic(_folder.Path, "IMG_4711.HEIC");

        var failure = await session.InsertAsync(iphone);

        failure.ShouldBe(new LoadFailure(iphone, LoadFailureReason.Unsupported));
        session.Inserted.ShouldBeNull();
        session.CanUndo.ShouldBeFalse();
    }

    private static async Task<PictureInPictureSession> OpenAsync(string mainImage)
    {
        var session = new PictureInPictureSession();
        (await session.OpenAsync(mainImage)).ShouldBeNull();
        return session;
    }

    private static async Task AssertColourAsync(string path, double x, double y, (int R, int G, int B) expected)
    {
        var actual = await SavedImage.ColourAtAsync(path, x, y);
        SavedImage.AreAlike(actual, expected).ShouldBeTrue($"Expected {expected} at ({x}, {y}), found {actual}");
    }
}
