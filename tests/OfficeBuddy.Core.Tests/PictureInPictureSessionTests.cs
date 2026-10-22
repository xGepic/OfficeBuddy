using OfficeBuddy.Core.Tests.Support;
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
        Assert.Equal(0.25, placement.Width, 6);
        Assert.Equal(1.0 / 3, session.HeightFor(placement.Width), 6);
        Assert.Equal(0.375, placement.Left, 6);
        Assert.Equal(1.0 / 3, placement.Top, 6);
    }

    [Fact]
    public async Task The_inserted_image_is_saved_where_it_was_placed_on_the_photo_at_the_photos_size()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue));
        await session.InsertAsync(await CreatePngAsync(_folder.Path, "Logo.png", 100, 100, Red));

        session.Place(new Placement(0.5, 0.5, 0.25));
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Equal("Urlaub (Bild in Bild).jpg", session.SuggestedFileName);
        var image = await SavedImage.ReadAsync(output);
        Assert.True(image.IsJpeg);
        Assert.Equal((400, 300), (image.Width, image.Height));
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
        Assert.Equal(0.75, placement.Left, 6);
        Assert.Equal(2.0 / 3, placement.Top, 6);

        // As big as it gets: as high as the photo.
        session.Place(new Placement(0, 0, 2));
        placement = session.Inserted!.Placement;
        Assert.Equal(0.75, placement.Width, 6);
        Assert.Equal(1, session.HeightFor(placement.Width), 6);
    }

    [Fact]
    public async Task A_PNG_stays_a_PNG()
    {
        var session = await OpenAsync(await CreatePngAsync(_folder.Path, "Plan.png", 300, 200, Green));
        await session.InsertAsync(await CreateJpegAsync(_folder.Path, "Foto.jpg", 100, 100, Red));

        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Equal("Plan (Bild in Bild).png", session.SuggestedFileName);
        var image = await SavedImage.ReadAsync(output);
        Assert.False(image.IsJpeg);
        Assert.Equal((300, 200), (image.Width, image.Height));
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
        Assert.Equal((300, 400), (image.Width, image.Height));
        Assert.True(image.Darkness.Right > 200, $"Expected the bar on the right, darkness {image.Darkness.Right}");
    }

    [Fact]
    public async Task Inserting_moving_and_removing_can_all_be_undone()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue));
        Assert.False(session.CanSave);

        await session.InsertAsync(await CreatePngAsync(_folder.Path, "Logo.png", 100, 100, Red));
        var start = session.Inserted!.Placement;
        session.Place(new Placement(0, 0, 0.1));
        session.Remove();
        Assert.Null(session.Inserted);
        Assert.False(session.CanSave);

        session.Undo();
        Assert.Equal(new Placement(0, 0, 0.1), session.Inserted!.Placement);
        session.Undo();
        Assert.Equal(start, session.Inserted!.Placement);
        Assert.True(session.CanSave);
        session.Undo();
        Assert.Null(session.Inserted);
        Assert.False(session.HasUnsavedChanges);
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

        Assert.Equal(0.375, session.Inserted!.Placement.Left, 6);
    }

    [Fact]
    public async Task Opening_another_photo_starts_over_without_an_inserted_image()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue));
        await session.InsertAsync(await CreatePngAsync(_folder.Path, "Logo.png", 100, 100, Red));

        await session.OpenAsync(await CreateJpegAsync(_folder.Path, "Garten.jpg", 300, 300, Green));

        Assert.Equal("Garten.jpg", session.MainImage!.FileName);
        Assert.Null(session.Inserted);
        Assert.False(session.CanUndo);
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task A_HEIC_photo_is_rejected_as_unsupported_and_changes_nothing()
    {
        var session = await OpenAsync(await CreateJpegAsync(_folder.Path, "Urlaub.jpg", 400, 300, Blue));
        var iphone = CreateHeic(_folder.Path, "IMG_4711.HEIC");

        var failure = await session.InsertAsync(iphone);

        Assert.Equal(new LoadFailure(iphone, LoadFailureReason.Unsupported), failure);
        Assert.Null(session.Inserted);
        Assert.False(session.CanUndo);
    }

    private static async Task<PictureInPictureSession> OpenAsync(string mainImage)
    {
        var session = new PictureInPictureSession();
        Assert.Null(await session.OpenAsync(mainImage));
        return session;
    }

    private static async Task AssertColourAsync(string path, double x, double y, (int R, int G, int B) expected)
    {
        var actual = await SavedImage.ColourAtAsync(path, x, y);
        Assert.True(SavedImage.AreAlike(actual, expected), $"Expected {expected} at ({x}, {y}), found {actual}");
    }
}
