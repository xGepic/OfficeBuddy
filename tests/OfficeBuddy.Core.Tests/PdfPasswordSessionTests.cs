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

        (await session.OpenAsync(statement, "geheim")).ShouldBeNull();
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        session.WasProtected.ShouldBeTrue();
        session.SuggestedFileName.ShouldBe("Kontoauszug (ohne Passwort).pdf");
        // Opens like any other Document again.
        var reopened = await Document.LoadAllAsync([output]);
        reopened.Failures.ShouldBeEmpty();
        SavedDocument.ReadLabels(output).ShouldBe(["P1"]);
    }

    [Fact]
    public async Task The_password_to_open_it_is_enough_even_when_the_owner_has_another_one()
    {
        // Like a bank statement: a password to open it, and an unknown one that forbids changes.
        var statement = SamplePdfs.CreateProtected(_folder.Path, "Kontoauszug.pdf", password: "1234", ownerPassword: "nur-die-bank");
        var session = new PdfPasswordSession();

        (await session.OpenAsync(statement, "1234")).ShouldBeNull();
        var output = _folder.File("Kontoauszug offen.pdf");
        await session.SaveAsync(output);

        (await Document.LoadAllAsync([output])).Failures.ShouldBeEmpty();
        SavedDocument.ReadLabels(output).ShouldBe(["P1"]);
    }

    [Fact]
    public async Task Without_its_password_or_with_a_wrong_one_a_Protected_Document_is_refused()
    {
        var statement = SamplePdfs.CreateProtected(_folder.Path, "Kontoauszug.pdf");
        var session = new PdfPasswordSession();

        (await session.OpenAsync(statement)).ShouldBe(new LoadFailure(statement, LoadFailureReason.Protected));
        (await session.OpenAsync(statement, "falsch")).ShouldBe(new LoadFailure(statement, LoadFailureReason.Protected));
        session.Document.ShouldBeNull();
    }

    [Fact]
    public async Task A_Document_gets_a_password_it_then_needs_to_open()
    {
        var findings = SamplePdfs.Create(_folder.Path, "Befund.pdf", "B1", "B2");
        var session = new PdfPasswordSession();
        (await session.OpenAsync(findings)).ShouldBeNull();
        session.WasProtected.ShouldBeFalse();
        session.CanSave.ShouldBeFalse();

        session.SetPassword("Sommer2026");
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        session.SuggestedFileName.ShouldBe("Befund (mit Passwort).pdf");
        (await Document.LoadAllAsync([output])).Failures.ShouldBe([new LoadFailure(output, LoadFailureReason.Protected)]);
        // And with it, it opens to the same Pages.
        var check = new PdfPasswordSession();
        (await check.OpenAsync(output, "Sommer2026")).ShouldBeNull();
        check.Document!.PageCount.ShouldBe(2);
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
        (await check.OpenAsync(output, "geheim")).ShouldNotBeNull();
        (await check.OpenAsync(output, "neu")).ShouldBeNull();
    }

    [Fact]
    public async Task Setting_the_password_can_be_undone_and_an_empty_one_means_none()
    {
        var findings = SamplePdfs.Create(_folder.Path, "Befund.pdf", "B1");
        var session = new PdfPasswordSession();
        await session.OpenAsync(findings);

        session.SetPassword("eins");
        session.SetPassword("");
        session.NewPassword.ShouldBeNull();
        session.Undo();
        session.NewPassword.ShouldBe("eins");
        session.Undo();
        session.HasUnsavedChanges.ShouldBeFalse();
    }
}
