using OfficeBuddy.Core.Tests.Support;

namespace OfficeBuddy.Core.Tests;

public sealed class RepairSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task A_Document_with_a_broken_index_is_rebuilt_with_all_its_Pages()
    {
        var invoice = SamplePdfs.CreateWithBrokenIndex(_folder.Path, "Rechnung.pdf", "R1", "R2", "R3");
        Assert.Equal(LoadFailureReason.Damaged, (await Document.LoadAllAsync([invoice])).Failures.Single().Reason);
        var session = new RepairSession();

        Assert.Null(await session.OpenAsync(invoice));
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.True(session.Result!.WasDamaged);
        Assert.True(session.CanSave);
        Assert.Equal("Rechnung (repariert).pdf", session.SuggestedFileName);
        Assert.Empty((await Document.LoadAllAsync([output])).Failures);
        Assert.Equal(["R1", "R2", "R3"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task A_Document_that_is_not_damaged_needs_no_repair()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "R1");
        var session = new RepairSession();

        Assert.Null(await session.OpenAsync(invoice));

        Assert.False(session.Result!.WasDamaged);
        Assert.False(session.CanSave);
    }

    [Fact]
    public async Task A_file_that_is_not_a_PDF_or_needs_a_password_is_refused()
    {
        var photo = SamplePdfs.CreateNotAPdf(_folder.Path, "Foto.jpg");
        var statement = SamplePdfs.CreateProtected(_folder.Path, "Kontoauszug.pdf");
        var session = new RepairSession();

        Assert.Equal(LoadFailureReason.Unsupported, (await session.OpenAsync(photo))!.Reason);
        Assert.Equal(LoadFailureReason.Protected, (await session.OpenAsync(statement))!.Reason);
        Assert.Null(session.Result);
    }

    [Fact]
    public async Task A_file_with_nothing_left_to_save_is_reported_as_damaged()
    {
        var empty = _folder.File("Leer.pdf");
        await File.WriteAllTextAsync(empty, "%PDF-1.7\n%%EOF\n");
        var session = new RepairSession();

        Assert.Equal(new LoadFailure(empty, LoadFailureReason.Damaged), await session.OpenAsync(empty));
        Assert.Null(session.Result);
    }
}
