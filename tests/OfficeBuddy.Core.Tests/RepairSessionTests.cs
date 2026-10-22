using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Pdf;
using OfficeBuddy.Core.Tests.Support;
using OfficeBuddy.Core.Tools;

namespace OfficeBuddy.Core.Tests;

public sealed class RepairSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task A_Document_with_a_broken_index_is_rebuilt_with_all_its_Pages()
    {
        var invoice = SamplePdfs.CreateWithBrokenIndex(_folder.Path, "Rechnung.pdf", "R1", "R2", "R3");
        (await Document.LoadAllAsync([invoice])).Failures.Single().Reason.ShouldBe(LoadFailureReason.Damaged);
        var session = new RepairSession();

        (await session.OpenAsync(invoice)).ShouldBeNull();
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        session.Result!.WasDamaged.ShouldBeTrue();
        session.CanSave.ShouldBeTrue();
        session.SuggestedFileName.ShouldBe("Rechnung (repariert).pdf");
        (await Document.LoadAllAsync([output])).Failures.ShouldBeEmpty();
        SavedDocument.ReadLabels(output).ShouldBe(["R1", "R2", "R3"]);
    }

    [Fact]
    public async Task A_Document_that_is_not_damaged_needs_no_repair()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "R1");
        var session = new RepairSession();

        (await session.OpenAsync(invoice)).ShouldBeNull();

        session.Result!.WasDamaged.ShouldBeFalse();
        session.CanSave.ShouldBeFalse();
    }

    [Fact]
    public async Task A_file_that_is_not_a_PDF_or_needs_a_password_is_refused()
    {
        var photo = SamplePdfs.CreateNotAPdf(_folder.Path, "Foto.jpg");
        var statement = SamplePdfs.CreateProtected(_folder.Path, "Kontoauszug.pdf");
        var session = new RepairSession();

        (await session.OpenAsync(photo))!.Reason.ShouldBe(LoadFailureReason.Unsupported);
        (await session.OpenAsync(statement))!.Reason.ShouldBe(LoadFailureReason.Protected);
        session.Result.ShouldBeNull();
    }

    [Fact]
    public async Task A_file_with_nothing_left_to_save_is_reported_as_damaged()
    {
        var empty = _folder.File("Leer.pdf");
        await File.WriteAllTextAsync(empty, "%PDF-1.7\n%%EOF\n");
        var session = new RepairSession();

        (await session.OpenAsync(empty)).ShouldBe(new LoadFailure(empty, LoadFailureReason.Damaged));
        session.Result.ShouldBeNull();
    }
}
