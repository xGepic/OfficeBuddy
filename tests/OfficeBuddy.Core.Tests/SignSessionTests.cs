using OfficeBuddy.Core.Tests.Support;
using static OfficeBuddy.Core.Tests.Support.SampleImages;

namespace OfficeBuddy.Core.Tests;

public sealed class SignSessionTests : IDisposable
{
    private static readonly (int R, int G, int B) White = (255, 255, 255);

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task The_signature_is_saved_on_the_chosen_Page_where_it_was_placed()
    {
        var contract = SamplePdfs.Create(_folder.Path, "Mietvertrag.pdf", "M1", "M2");
        var session = await OpenAsync(contract, await CreatePngAsync(_folder.Path, "Unterschrift.png", 200, 100, Blue));

        session.Sign(1);
        session.Place(1, new Placement(0.1, 0.1, 0.25));
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Equal("Mietvertrag (unterschrieben).pdf", session.SuggestedFileName);
        Assert.Equal(["M1", "M2"], SavedDocument.ReadLabels(output));
        var middle = (0.1 + 0.125, 0.1 + session.HeightFor(1, 0.25) / 2);
        AssertAlike(Blue, (await SavedDocument.RenderAsync(output, 1)).ColourAt(middle));
        AssertAlike(White, (await SavedDocument.RenderAsync(output, 1)).ColourAt((0.8, 0.1)));
        AssertAlike(White, (await SavedDocument.RenderAsync(output, 0)).ColourAt(middle));
    }

    [Theory]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public async Task On_a_turned_Page_the_signature_lands_where_it_was_seen(int rotation)
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", new SamplePage("S1", Rotation: rotation));
        var session = await OpenAsync(scan, await CreatePngAsync(_folder.Path, "Unterschrift.png", 200, 100, Blue));

        // Top left of the Page as shown, which is a different corner of the unturned Page each time.
        session.Sign(0);
        session.Place(0, new Placement(0.05, 0.05, 0.25));
        var output = _folder.File("Scan unterschrieben.pdf");
        await session.SaveAsync(output);

        var page = await SavedDocument.RenderAsync(output, 0);
        AssertAlike(Blue, page.ColourAt((0.05 + 0.125, 0.05 + session.HeightFor(0, 0.25) / 2)));
        AssertAlike(White, page.ColourAt((0.9, 0.9)));
        AssertAlike(White, page.ColourAt((0.9, 0.1)));
        AssertAlike(White, page.ColourAt((0.1, 0.9)));
    }

    [Fact]
    public async Task The_paper_around_a_signature_photographed_on_white_paper_lets_the_Page_show_through()
    {
        // A black bar along the top of the Page, 60 of its 842 points high.
        var form = SamplePdfs.CreateWithTopBar(_folder.Path, "Antrag.pdf", rotation: 0);
        var session = await OpenAsync(form, await CreateSignatureAsync(_folder.Path, "Unterschrift.jpg", 200, 60, onWhitePaper: true));

        session.Sign(0);
        session.Place(0, new Placement(0.3, 0, 0.5));
        var output = _folder.File("Antrag unterschrieben.pdf");
        await session.SaveAsync(output);

        // The signature's top third is white paper over the bar: the bar must stay black there.
        var page = await SavedDocument.RenderAsync(output, 0);
        AssertAlike((0, 0, 0), page.ColourAt((0.55, 0.012)));
        // Below the bar, its stroke is black on a white Page, its paper invisible.
        var height = session.HeightFor(0, 0.5);
        AssertAlike(White, page.ColourAt((0.55, height * 0.9)));
    }

    [Fact]
    public async Task The_signature_always_stays_whole_on_the_Page()
    {
        var contract = SamplePdfs.Create(_folder.Path, "Mietvertrag.pdf", "M1");
        var session = await OpenAsync(contract, await CreatePngAsync(_folder.Path, "Unterschrift.png", 200, 100, Blue));
        session.Sign(0);

        session.Place(0, new Placement(0.9, 0.95, 0.25));
        var placement = session.Placements[0];
        Assert.Equal(0.75, placement.Left, 6);
        Assert.Equal(1 - session.HeightFor(0, 0.25), placement.Top, 6);

        session.Place(0, new Placement(0, 0, 5));
        Assert.Equal(1, session.Placements[0].Width, 6);
    }

    [Fact]
    public async Task A_new_signature_starts_bottom_right_and_every_change_can_be_undone()
    {
        var contract = SamplePdfs.Create(_folder.Path, "Mietvertrag.pdf", "M1", "M2");
        var session = await OpenAsync(contract, await CreatePngAsync(_folder.Path, "Unterschrift.png", 200, 100, Blue));
        Assert.False(session.CanSave);

        session.Sign(0);
        var start = session.Placements[0];
        Assert.True(start.Left > 0.5 && start.Top > 0.5, $"Expected bottom right, was {start}");
        session.Sign(1);
        session.Unsign(0);
        Assert.Equal([1], session.Placements.Keys);
        Assert.True(session.CanSave);

        session.Undo();
        session.Undo();
        Assert.Equal([0], session.Placements.Keys);
        session.Undo();
        Assert.Empty(session.Placements);
    }

    [Fact]
    public async Task The_signature_stays_chosen_for_the_next_Document()
    {
        var first = SamplePdfs.Create(_folder.Path, "Vertrag.pdf", "V1");
        var second = SamplePdfs.Create(_folder.Path, "Antrag.pdf", "A1");
        var session = await OpenAsync(first, await CreatePngAsync(_folder.Path, "Unterschrift.png", 200, 100, Blue));
        session.Sign(0);

        await session.OpenAsync(second);

        Assert.Equal("Unterschrift.png", session.Signature!.FileName);
        Assert.Empty(session.Placements);
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task The_signature_chosen_last_time_is_ready_without_being_a_change()
    {
        var contract = SamplePdfs.Create(_folder.Path, "Mietvertrag.pdf", "M1");
        var remembered = await CreatePngAsync(_folder.Path, "Unterschrift.png", 200, 100, Blue);
        var session = new SignSession();

        await session.OfferSignatureAsync(remembered);
        await session.OpenAsync(contract);

        Assert.Equal("Unterschrift.png", session.Signature!.FileName);
        Assert.False(session.CanUndo);
        Assert.False(session.HasUnsavedChanges);

        // Once one is chosen, an offer changes nothing.
        await session.ChooseSignatureAsync(await CreatePngAsync(_folder.Path, "Neu.png", 200, 100, Green));
        await session.OfferSignatureAsync(remembered);
        Assert.Equal("Neu.png", session.Signature!.FileName);
    }

    [Fact]
    public async Task A_Document_whose_owner_forbids_changes_can_still_be_signed()
    {
        // Opens without a password, but changes need the owner's, which the user doesn't know.
        var form = SamplePdfs.CreateProtected(_folder.Path, "Formular.pdf", password: "", ownerPassword: "amt");
        var session = await OpenAsync(form, await CreatePngAsync(_folder.Path, "Unterschrift.png", 200, 100, Blue));

        session.Sign(0);
        var output = _folder.File("Formular unterschrieben.pdf");
        await session.SaveAsync(output);

        Assert.Equal(["P1"], SavedDocument.ReadLabels(output));
    }

    private static async Task<SignSession> OpenAsync(string document, string signature)
    {
        var session = new SignSession();
        Assert.Null(await session.OpenAsync(document));
        Assert.Null(await session.ChooseSignatureAsync(signature));
        return session;
    }

    private static void AssertAlike((int R, int G, int B) expected, (int R, int G, int B) actual) =>
        Assert.True(SavedImage.AreAlike(actual, expected), $"Expected {expected}, found {actual}");
}
