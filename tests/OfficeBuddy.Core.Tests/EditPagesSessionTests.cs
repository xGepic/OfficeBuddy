using OfficeBuddy.Core.Tests.Support;

namespace OfficeBuddy.Core.Tests;

public sealed class EditPagesSessionTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Saving_an_unchanged_Document_keeps_all_its_Pages_in_order()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2", "S3");
        var session = new EditPagesSession();

        await session.OpenAsync(scan);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(["S1", "S2", "S3"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task Pages_of_another_Document_are_inserted_at_the_chosen_place()
    {
        var letter = SamplePdfs.Create(_folder.Path, "Brief.pdf", "B1", "B2", "B3");
        var attachment = SamplePdfs.Create(_folder.Path, "Anlage.pdf", "A1", "A2");
        var session = new EditPagesSession();
        await session.OpenAsync(letter);

        var failures = await session.InsertAsync([attachment], position: 1);
        var output = _folder.File(session.SuggestedFileName!);
        await session.SaveAsync(output);

        Assert.Empty(failures);
        Assert.Equal(["B1", "A1", "A2", "B2", "B3"], SavedDocument.ReadLabels(output));
        // Named after the Document that was opened, not the inserted one.
        Assert.Equal("Brief (bearbeitet).pdf", session.SuggestedFileName);
        Assert.Equal("Anlage.pdf", session.Pages[1].InsertedFrom!.FileName);
        Assert.Null(session.Pages[0].InsertedFrom);
    }

    [Fact]
    public async Task Inserted_Pages_can_be_rotated_moved_and_removed_like_the_others()
    {
        var letter = SamplePdfs.Create(_folder.Path, "Brief.pdf", "B1", "B2");
        var attachment = SamplePdfs.Create(_folder.Path, "Anlage.pdf", "A1", "A2");
        var session = new EditPagesSession();
        await session.OpenAsync(letter);
        await session.InsertAsync([attachment], position: 2);

        session.RotateRight([2]);
        session.MarkForRemoval([3]);
        session.Move(2, 0);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal([new SavedPage("A1", 90), new SavedPage("B1", 0), new SavedPage("B2", 0)], SavedDocument.ReadPages(output));
    }

    [Fact]
    public async Task Several_Documents_are_inserted_in_order_and_one_that_cannot_be_opened_is_reported()
    {
        var letter = SamplePdfs.Create(_folder.Path, "Brief.pdf", "B1");
        var first = SamplePdfs.Create(_folder.Path, "Anlage 1.pdf", "A1");
        var second = SamplePdfs.Create(_folder.Path, "Anlage 2.pdf", "C1");
        var notAPdf = _folder.File("Notiz.txt");
        await File.WriteAllTextAsync(notAPdf, "Hallo");
        var session = new EditPagesSession();
        await session.OpenAsync(letter);

        var failures = await session.InsertAsync([first, notAPdf, second], position: 1);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal([new LoadFailure(notAPdf, LoadFailureReason.Unsupported)], failures);
        Assert.Equal(["B1", "A1", "C1"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task The_same_Document_can_be_inserted_into_itself()
    {
        var letter = SamplePdfs.Create(_folder.Path, "Brief.pdf", "B1", "B2");
        var session = new EditPagesSession();
        await session.OpenAsync(letter);

        await session.InsertAsync([letter], position: 2);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(["B1", "B2", "B1", "B2"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task Inserting_is_one_change_to_undo_and_a_file_that_cannot_be_opened_changes_nothing()
    {
        var letter = SamplePdfs.Create(_folder.Path, "Brief.pdf", "B1", "B2");
        var attachment = SamplePdfs.Create(_folder.Path, "Anlage.pdf", "A1", "A2");
        var session = new EditPagesSession();
        await session.OpenAsync(letter);

        await session.InsertAsync([attachment], position: 0);
        Assert.Equal(4, session.Pages.Count);
        Assert.True(session.HasUnsavedChanges);
        session.Undo();
        Assert.Equal(2, session.Pages.Count);
        Assert.False(session.HasUnsavedChanges);

        var notAPdf = _folder.File("Notiz.txt");
        await File.WriteAllTextAsync(notAPdf, "Hallo");
        await session.InsertAsync([notAPdf], position: 0);
        Assert.Equal(2, session.Pages.Count);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public async Task Pages_marked_for_removal_are_left_out_when_saved()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2", "S3", "S4");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);

        session.MarkForRemoval([1, 3]);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(["S1", "S3"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task An_unmarked_Page_is_kept_again()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2", "S3");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);

        session.MarkForRemoval([0, 2]);
        session.Unmark([2]);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(["S2", "S3"], SavedDocument.ReadLabels(output));
        Assert.Equal([true, false, false], session.Pages.Select(page => page.IsMarkedForRemoval));
    }

    [Fact]
    public async Task Rotating_by_90_degrees_adds_up_over_repeated_rotations()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2", "S3", "S4");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);

        session.RotateRight([0, 2]);
        session.RotateLeft([1]);
        session.RotateRight([2]);
        session.RotateLeft([3]);
        session.RotateRight([3]);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(
            [new SavedPage("S1", 90), new SavedPage("S2", 270), new SavedPage("S3", 180), new SavedPage("S4", 0)],
            SavedDocument.ReadPages(output));
    }

    [Fact]
    public async Task A_sideways_scanned_Page_is_turned_upright_on_top_of_its_own_rotation()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf",
            new SamplePage("S1"), new SamplePage("S2", Sideways: true), new SamplePage("S3", Rotation: 270));
        var session = new EditPagesSession();
        await session.OpenAsync(scan);

        session.RotateRight([1, 2]);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(
            [new SavedPage("S1", 0), new SavedPage("S2", 90), new SavedPage("S3", 0)],
            SavedDocument.ReadPages(output));
    }

    [Fact]
    public async Task Moving_a_Page_reorders_the_saved_Document()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2", "S3", "S4");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);

        session.Move(from: 3, to: 0);
        session.Move(from: 1, to: 2);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(["S4", "S2", "S1", "S3"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task Removing_page_2_and_rotating_page_3_then_undoing_the_rotation_saves_only_the_removal()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2", "S3", "S4");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);

        session.MarkForRemoval([1]);
        session.RotateRight([2]);
        session.Undo();
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(
            [new SavedPage("S1", 0), new SavedPage("S3", 0), new SavedPage("S4", 0)],
            SavedDocument.ReadPages(output));
    }

    [Fact]
    public async Task Undoing_a_move_and_an_unmark_restores_order_and_mark()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2", "S3");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);
        session.MarkForRemoval([0]);

        session.Unmark([0]);
        session.Move(from: 0, to: 2);
        session.Undo();
        session.Undo();
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(["S2", "S3"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task Saving_is_blocked_while_every_Page_is_marked_for_removal()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2");
        var session = new EditPagesSession();
        Assert.False(session.CanSave);

        await session.OpenAsync(scan);
        Assert.True(session.CanSave);

        session.MarkForRemoval([0, 1]);
        Assert.False(session.CanSave);
        Assert.True(session.EveryPageMarkedForRemoval);

        session.Unmark([1]);
        Assert.True(session.CanSave);
        Assert.False(session.EveryPageMarkedForRemoval);
    }

    [Fact]
    public async Task The_suggested_name_and_folder_come_from_the_Document()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Rechnung.pdf", "S1");
        var session = new EditPagesSession();
        Assert.Null(session.SuggestedFileName);

        await session.OpenAsync(scan);

        Assert.Equal("Rechnung (bearbeitet).pdf", session.SuggestedFileName);
        Assert.Equal(_folder.Path, session.SuggestedFolder);
    }

    [Fact]
    public async Task A_Protected_Document_is_reported_and_the_open_Document_stays()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2");
        var statement = SamplePdfs.CreateProtected(_folder.Path, "Kontoauszug.pdf");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);
        session.MarkForRemoval([0]);

        var failure = await session.OpenAsync(statement);

        Assert.Equal(new LoadFailure(statement, LoadFailureReason.Protected), failure);
        Assert.Equal("Scan.pdf", session.Document?.FileName);
        Assert.True(session.CanUndo);
    }

    [Fact]
    public async Task Opening_another_Document_starts_over_with_nothing_to_undo_or_save()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2");
        var letter = SamplePdfs.Create(_folder.Path, "Brief.pdf", "B1", "B2", "B3");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);
        session.RotateRight([0]);

        await session.OpenAsync(letter);

        Assert.Equal([0, 1, 2], session.Pages.Select(page => page.SourceIndex));
        Assert.False(session.CanUndo);
        Assert.False(session.HasUnsavedChanges);
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);
        Assert.Equal(
            [new SavedPage("B1", 0), new SavedPage("B2", 0), new SavedPage("B3", 0)],
            SavedDocument.ReadPages(output));
    }

    [Fact]
    public async Task The_edited_Document_can_overwrite_its_source()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2", "S3");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);

        session.MarkForRemoval([1]);
        await session.SaveAsync(scan);

        Assert.Equal(["S1", "S3"], SavedDocument.ReadLabels(scan));
    }

    [Fact]
    public async Task A_Page_is_rendered_to_fit_the_requested_size_with_its_own_rotation()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", new SamplePage("S1"), new SamplePage("S2", Rotation: 90));
        var session = new EditPagesSession();
        await session.OpenAsync(scan);

        var upright = PngSize.Read(await session.Document!.RenderPageAsPngAsync(0, maxWidth: 300, maxHeight: 300));
        var turned = PngSize.Read(await session.Document!.RenderPageAsPngAsync(1, maxWidth: 300, maxHeight: 300));

        // An A4 Page is 1 : 1.414, so its long side fills the box and the short one is 212 pixels.
        Assert.Equal((212, 300), upright);
        Assert.Equal((300, 212), turned);
    }

    [Fact]
    public async Task Undoing_a_removal_mark_keeps_the_Page()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);

        session.MarkForRemoval([1]);
        session.Undo();
        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(["S1", "S2"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task Removal_can_be_taken_back_only_when_every_selected_Page_is_marked()
    {
        var scan = SamplePdfs.Create(_folder.Path, "Scan.pdf", "S1", "S2", "S3");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);

        session.MarkForRemoval([0, 2]);

        Assert.True(session.AreAllMarkedForRemoval([0, 2]));
        Assert.False(session.AreAllMarkedForRemoval([0, 1]));
        Assert.False(session.AreAllMarkedForRemoval([]));
    }

    [Fact]
    public async Task Opening_a_duplex_scan_marks_its_blank_backs_for_removal()
    {
        var scan = SamplePdfs.CreateDuplexScan(_folder.Path, "Scan.pdf");
        var session = new EditPagesSession();

        await session.OpenAsync(scan);

        Assert.Equal([2, 6, 8], session.DetectedBlankPageNumbers);
        Assert.Equal([1, 5, 7], session.Pages.Where(page => page.IsMarkedForRemoval).Select(page => page.SourceIndex));
    }

    [Fact]
    public async Task Pages_with_only_a_signature_a_stamp_or_a_faint_pencil_note_are_not_taken_for_blank()
    {
        var scan = SamplePdfs.CreateDuplexScan(_folder.Path, "Scan.pdf");
        var session = new EditPagesSession();

        await session.OpenAsync(scan);

        Assert.False(session.Pages[3].IsMarkedForRemoval);
        Assert.False(session.Pages[6].IsMarkedForRemoval);
        Assert.False(session.Pages[8].IsMarkedForRemoval);
    }

    [Fact]
    public async Task Saving_a_duplex_scan_without_changes_drops_exactly_the_blank_backs()
    {
        var scan = SamplePdfs.CreateDuplexScan(_folder.Path, "Scan.pdf");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);

        var output = _folder.File("Ergebnis.pdf");
        await session.SaveAsync(output);

        Assert.Equal(["S1", "S2", "U1", "S3", "BEZAHLT", "N1"], SavedDocument.ReadLabels(output));
    }

    [Fact]
    public async Task Detected_Blank_pages_are_the_starting_point_and_unmarking_one_can_be_undone()
    {
        var scan = SamplePdfs.CreateDuplexScan(_folder.Path, "Scan.pdf");
        var session = new EditPagesSession();
        await session.OpenAsync(scan);
        Assert.False(session.CanUndo);
        Assert.False(session.HasUnsavedChanges);

        session.Unmark([1]);
        Assert.False(session.Pages[1].IsMarkedForRemoval);

        session.Undo();
        Assert.True(session.Pages[1].IsMarkedForRemoval);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public async Task A_Document_without_Blank_pages_opens_with_nothing_marked()
    {
        var letter = SamplePdfs.Create(_folder.Path, "Brief.pdf", "S1", "S2", "S3");
        var session = new EditPagesSession();

        await session.OpenAsync(letter);

        Assert.Empty(session.DetectedBlankPageNumbers);
        Assert.DoesNotContain(session.Pages, page => page.IsMarkedForRemoval);
    }

    [Fact]
    public async Task A_very_narrow_Page_does_not_stop_the_Document_from_opening()
    {
        var strip = SamplePdfs.CreateNarrowStrip(_folder.Path, "Bon.pdf");
        var session = new EditPagesSession();

        var failure = await session.OpenAsync(strip);

        Assert.Null(failure);
        Assert.Single(session.Pages);
    }
}
