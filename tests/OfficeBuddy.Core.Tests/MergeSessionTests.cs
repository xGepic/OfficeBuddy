using OfficeBuddy.Core.Files;
using OfficeBuddy.Core.Tests.Support;
using OfficeBuddy.Core.Tools;

namespace OfficeBuddy.Core.Tests;

public sealed class MergeSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Merging_two_Documents_saves_all_their_Pages_in_list_order()
    {
        var first = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "A1", "A2");
        var second = SamplePdfs.Create(_folder.Path, "Brief.pdf", "B1", "B2", "B3");
        var session = new MergeSession();

        await session.AddAsync([first, second]);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        SavedDocument.ReadLabels(output).ShouldBe(["A1", "A2", "B1", "B2", "B3"]);
    }

    [Fact]
    public async Task A_file_named_PDF_without_PDF_content_is_reported_as_damaged_and_the_others_are_still_added()
    {
        var first = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var broken = _folder.File("Kaputt.pdf");
        await File.WriteAllTextAsync(broken, "Das ist keine PDF-Datei.");
        var last = SamplePdfs.Create(_folder.Path, "C.pdf", "C1");
        var session = new MergeSession();

        var failed = await session.AddAsync([first, broken, last]);

        failed.ShouldBe([new LoadFailure(broken, LoadFailureReason.Damaged)]);
        session.Documents.Select(d => d.FileName).ShouldBe(["A.pdf", "C.pdf"]);
    }

    [Fact]
    public async Task A_Protected_Document_is_reported_as_protected_and_not_added()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "A1");
        var statement = SamplePdfs.CreateProtected(_folder.Path, "Kontoauszug.pdf");
        var session = new MergeSession();

        var failed = await session.AddAsync([invoice, statement]);

        failed.ShouldBe([new LoadFailure(statement, LoadFailureReason.Protected)]);
        session.Documents.Select(d => d.FileName).ShouldBe(["Rechnung.pdf"]);
    }

    [Fact]
    public async Task A_damaged_PDF_is_reported_as_damaged_and_not_added()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "A1");
        var scan = SamplePdfs.CreateDamaged(_folder.Path, "Scan.pdf");
        var session = new MergeSession();

        var failed = await session.AddAsync([invoice, scan]);

        failed.ShouldBe([new LoadFailure(scan, LoadFailureReason.Damaged)]);
        session.Documents.Select(d => d.FileName).ShouldBe(["Rechnung.pdf"]);
    }

    [Fact]
    public async Task A_file_that_is_not_a_PDF_is_reported_as_unsupported_and_not_added()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "A1");
        var photo = SamplePdfs.CreateNotAPdf(_folder.Path, "Foto.jpg");
        var session = new MergeSession();

        var failed = await session.AddAsync([invoice, photo]);

        failed.ShouldBe([new LoadFailure(photo, LoadFailureReason.Unsupported)]);
        session.Documents.Select(d => d.FileName).ShouldBe(["Rechnung.pdf"]);
    }

    [Fact]
    public async Task Moving_a_Document_changes_where_its_Pages_end_up()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var c = SamplePdfs.Create(_folder.Path, "C.pdf", "C1", "C2");
        var session = new MergeSession();
        await session.AddAsync([a, b, c]);

        session.Move(from: 2, to: 0);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        session.Documents.Select(d => d.FileName).ShouldBe(["C.pdf", "A.pdf", "B.pdf"]);
        SavedDocument.ReadLabels(output).ShouldBe(["C1", "C2", "A1", "B1"]);
    }

    [Fact]
    public async Task A_removed_Document_is_left_out_of_the_merge()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var c = SamplePdfs.Create(_folder.Path, "C.pdf", "C1");
        var session = new MergeSession();
        await session.AddAsync([a, b, c]);

        session.Remove(1);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        SavedDocument.ReadLabels(output).ShouldBe(["A1", "C1"]);
    }

    [Fact]
    public async Task Saving_needs_at_least_two_Documents()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var session = new MergeSession();
        session.CanSave.ShouldBeFalse();

        await session.AddAsync([a]);
        session.CanSave.ShouldBeFalse();

        await session.AddAsync([b]);
        session.CanSave.ShouldBeTrue();

        session.Remove(0);
        session.CanSave.ShouldBeFalse();
    }

    [Fact]
    public async Task The_suggested_name_and_folder_come_from_the_first_Document_in_the_list()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "A1");
        var otherFolder = Directory.CreateDirectory(_folder.File("Briefe")).FullName;
        var letter = SamplePdfs.Create(otherFolder, "Brief.pdf", "B1");
        var session = new MergeSession();
        await session.AddAsync([invoice, letter]);

        session.SuggestedFileName.ShouldBe("Rechnung (zusammengefügt).pdf");
        session.SuggestedFolder.ShouldBe(_folder.Path);

        session.Move(from: 1, to: 0);

        session.SuggestedFileName.ShouldBe("Brief (zusammengefügt).pdf");
        session.SuggestedFolder.ShouldBe(otherFolder);
    }

    [Fact]
    public async Task Pages_keep_their_rotation_when_merged()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf",
            new SamplePage("A1"), new SamplePage("A2", Rotation: 90), new SamplePage("A3", Rotation: 270));
        var letter = SamplePdfs.Create(_folder.Path, "Brief.pdf", new SamplePage("B1", Rotation: 180));
        var session = new MergeSession();
        await session.AddAsync([scan, letter]);

        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        SavedDocument.ReadPages(output).ShouldBe([new SavedPage("A1", 0), new SavedPage("A2", 90), new SavedPage("A3", 270), new SavedPage("B1", 180)]);
    }

    [Fact]
    public async Task Changing_the_list_while_saving_does_not_change_the_saved_Document()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var session = new MergeSession();
        await session.AddAsync([a, b]);

        var output = _folder.File("Ergebnis.pdf");
        var saving = session.SaveAsync(output);
        session.Remove(0);
        await saving;

        SavedDocument.ReadLabels(output).ShouldBe(["A1", "B1"]);
    }

    [Fact]
    public async Task The_merged_Document_can_overwrite_one_of_its_sources()
    {
        var invoice = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "A1", "A2");
        var letter = SamplePdfs.Create(_folder.Path, "Brief.pdf", "B1");
        var session = new MergeSession();
        await session.AddAsync([invoice, letter]);

        await session.SaveAsync(invoice);

        SavedDocument.ReadLabels(invoice).ShouldBe(["A1", "A2", "B1"]);
    }

    [Fact]
    public async Task Undoing_a_removal_brings_the_Document_back()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var c = SamplePdfs.Create(_folder.Path, "C.pdf", "C1");
        var session = new MergeSession();
        await session.AddAsync([a, b, c]);

        session.Remove(1);
        session.Undo();
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        SavedDocument.ReadLabels(output).ShouldBe(["A1", "B1", "C1"]);
    }

    [Fact]
    public async Task Redo_repeats_an_undone_move()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var session = new MergeSession();
        await session.AddAsync([a, b]);

        session.Move(from: 1, to: 0);
        session.Undo();
        session.Redo();
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        SavedDocument.ReadLabels(output).ShouldBe(["B1", "A1"]);
    }

    [Fact]
    public async Task A_new_change_after_undo_discards_what_could_be_redone()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var c = SamplePdfs.Create(_folder.Path, "C.pdf", "C1");
        var session = new MergeSession();
        await session.AddAsync([a, b, c]);

        session.Remove(0);
        session.Undo();
        session.CanRedo.ShouldBeTrue();

        session.Remove(2);

        session.CanRedo.ShouldBeFalse();
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);
        SavedDocument.ReadLabels(output).ShouldBe(["A1", "B1"]);
    }

    [Fact]
    public async Task Adding_Documents_can_be_undone_and_redone()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var session = new MergeSession();
        session.CanUndo.ShouldBeFalse();
        session.CanRedo.ShouldBeFalse();

        await session.AddAsync([a, b]);
        session.CanUndo.ShouldBeTrue();

        session.Undo();
        session.Documents.ShouldBeEmpty();
        session.CanUndo.ShouldBeFalse();
        session.CanRedo.ShouldBeTrue();

        session.Redo();
        session.Documents.Select(d => d.FileName).ShouldBe(["A.pdf", "B.pdf"]);
        session.CanUndo.ShouldBeTrue();
        session.CanRedo.ShouldBeFalse();
    }

    [Fact]
    public async Task Opening_only_files_that_cannot_be_opened_is_not_a_change_to_undo()
    {
        var photo = SamplePdfs.CreateNotAPdf(_folder.Path, "Foto.jpg");
        var session = new MergeSession();

        await session.AddAsync([photo]);

        session.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public async Task There_are_unsaved_changes_after_any_change_until_it_is_saved()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var session = new MergeSession();
        session.HasUnsavedChanges.ShouldBeFalse();

        await session.AddAsync([a, b]);
        session.HasUnsavedChanges.ShouldBeTrue();

        await session.SaveAsync(_folder.File("Ergebnis.pdf"));
        session.HasUnsavedChanges.ShouldBeFalse();

        session.Move(from: 1, to: 0);
        session.HasUnsavedChanges.ShouldBeTrue();
    }

    [Fact]
    public async Task Saving_clears_the_undo_history()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var c = SamplePdfs.Create(_folder.Path, "C.pdf", "C1");
        var session = new MergeSession();
        await session.AddAsync([a, b, c]);
        session.Remove(2);
        session.Remove(1);
        session.Undo();

        await session.SaveAsync(_folder.File("Ergebnis.pdf"));

        session.CanUndo.ShouldBeFalse();
        session.CanRedo.ShouldBeFalse();
    }

    [Fact]
    public async Task Undoing_every_change_since_saving_leaves_nothing_unsaved()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var session = new MergeSession();
        await session.AddAsync([a, b]);
        await session.SaveAsync(_folder.File("Ergebnis.pdf"));

        session.Remove(0);
        session.Undo();

        session.HasUnsavedChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task Undo_and_redo_with_nothing_to_take_back_change_nothing()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var session = new MergeSession();
        await session.AddAsync([a]);

        session.Redo();
        session.Undo();
        session.Undo();

        session.Documents.ShouldBeEmpty();
        session.CanRedo.ShouldBeTrue();
    }

    [Fact]
    public async Task The_screen_is_told_about_every_change_undo_redo_and_save()
    {
        var a = SamplePdfs.Create(_folder.Path, "A.pdf", "A1");
        var b = SamplePdfs.Create(_folder.Path, "B.pdf", "B1");
        var session = new MergeSession();
        var told = false;
        session.Changed += (_, _) => told = true;

        async Task AssertTold(Func<Task> action)
        {
            told = false;
            await action();
            told.ShouldBeTrue();
        }

        await AssertTold(() => session.AddAsync([a, b]));
        await AssertTold(() => { session.Move(from: 1, to: 0); return Task.CompletedTask; });
        await AssertTold(() => { session.Remove(0); return Task.CompletedTask; });
        await AssertTold(() => { session.Undo(); return Task.CompletedTask; });
        await AssertTold(() => { session.Redo(); return Task.CompletedTask; });
        await AssertTold(() => session.SaveAsync(_folder.File("Ergebnis.pdf")));
    }
}
