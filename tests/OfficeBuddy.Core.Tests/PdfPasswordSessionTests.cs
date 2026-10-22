using OfficeBuddy.Core.Tests.Support;

namespace OfficeBuddy.Core.Tests;

public sealed class PdfPasswordSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task A_Protected_Document_is_saved_without_its_password()
    {
        var statement = SamplePdfs.CreateProtected(_folder.Path, "Kontoauszug.pdf");
        var session = new PdfPasswordSession();

        Assert.Null(await session.OpenAsync(statement, "geheim"));
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.True(session.WasProtected);
        Assert.Equal("Kontoauszug (ohne Passwort).pdf", session.SuggestedFileName);
        // Opens like any other Document again.
        var reopened = await Document.LoadAllAsync([output]);
        Assert.Empty(reopened.Failures);
        Assert.Equal(["P1"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task The_password_to_open_it_is_enough_even_when_the_owner_has_another_one()
    {
        // Like a bank statement: a password to open it, and an unknown one that forbids changes.
        var statement = SamplePdfs.CreateProtected(_folder.Path, "Kontoauszug.pdf", password: "1234", ownerPassword: "nur-die-bank");
        var session = new PdfPasswordSession();

        Assert.Null(await session.OpenAsync(statement, "1234"));
        var output = _folder.File("Kontoauszug offen.pdf");
        await session.SaveAsync(output);

        Assert.Empty((await Document.LoadAllAsync([output])).Failures);
        Assert.Equal(["P1"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task Without_its_password_or_with_a_wrong_one_a_Protected_Document_is_refused()
    {
        var statement = SamplePdfs.CreateProtected(_folder.Path, "Kontoauszug.pdf");
        var session = new PdfPasswordSession();

        Assert.Equal(new LoadFailure(statement, LoadFailureReason.Protected), await session.OpenAsync(statement));
        Assert.Equal(new LoadFailure(statement, LoadFailureReason.Protected), await session.OpenAsync(statement, "falsch"));
        Assert.Null(session.Document);
    }

    [Fact]
    public async Task A_Document_gets_a_password_it_then_needs_to_open()
    {
        var findings = SamplePdfs.Create(_folder.Path, "Befund.pdf", "B1", "B2");
        var session = new PdfPasswordSession();
        Assert.Null(await session.OpenAsync(findings));
        Assert.False(session.WasProtected);
        Assert.False(session.CanSave);

        session.SetPassword("Sommer2026");
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Equal("Befund (mit Passwort).pdf", session.SuggestedFileName);
        Assert.Equal([new LoadFailure(output, LoadFailureReason.Protected)], (await Document.LoadAllAsync([output])).Failures);
        // And with it, it opens to the same Pages.
        var check = new PdfPasswordSession();
        Assert.Null(await check.OpenAsync(output, "Sommer2026"));
        Assert.Equal(2, check.Document!.PageCount);
    }

    [Fact]
    public async Task A_Protected_Document_can_get_a_new_password_instead()
    {
        var statement = SamplePdfs.CreateProtected(_folder.Path, "Kontoauszug.pdf");
        var session = new PdfPasswordSession();
        await session.OpenAsync(statement, "geheim");

        session.SetPassword("neu");
        var output = _folder.File("Kontoauszug neu.pdf");
        await session.SaveAsync(output);

        var check = new PdfPasswordSession();
        Assert.NotNull(await check.OpenAsync(output, "geheim"));
        Assert.Null(await check.OpenAsync(output, "neu"));
    }

    [Fact]
    public async Task Setting_the_password_can_be_undone_and_an_empty_one_means_none()
    {
        var findings = SamplePdfs.Create(_folder.Path, "Befund.pdf", "B1");
        var session = new PdfPasswordSession();
        await session.OpenAsync(findings);

        session.SetPassword("eins");
        session.SetPassword("");
        Assert.Null(session.NewPassword);
        session.Undo();
        Assert.Equal("eins", session.NewPassword);
        session.Undo();
        Assert.False(session.HasUnsavedChanges);
    }
}
