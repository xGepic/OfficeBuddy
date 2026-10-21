# PDF-Manager v1: Tickets

Tickets for [the v1 spec](0001-pdf-manager-v1.md), numbered in dependency order. Vocabulary follows [GLOSSARY.md](../../GLOSSARY.md); respect [ADR-0001](../adr/0001-ghostscript-for-compression.md) and [ADR-0002](../adr/0002-one-tool-per-tile.md).

Any ticket whose blockers are all done can be started.

```
01 ─┬─ 02
    ├─ 03 ─┬─ 04 ─┬─ 05
    │      │      ├─ 06
    │      │      └─ 07 ─┬─ 08
    │      │             └─ 10
    │      └─ 11
    └─ 09 ─┬─ 10
           └─ 12 (also needs 01)
```

---

# 01: App shell with a basic Merge (first working version)

**What to build:** The first version of the app that runs end to end. Opening PDF-Manager shows a German start screen with all seven Tool tiles. *Zusammenfügen* opens a working Merge screen, and the other tiles are visible but not yet usable. In Merge the parent opens two or more Documents (with *Öffnen*, by dragging files into the screen, or by dropping a PDF onto the tile), sees them as a list with name and page count, drags them into order, removes one if needed, and saves the merged Document through a pre-filled save dialog. This ticket also sets up the shared parts every later Tool plugs into: the Tool screen frame, opening Documents, single-file saving, and the session pattern in Core.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The solution has three projects: Core (Windows-specific .NET 10, no UI dependencies), App (unpackaged, self-contained WinUI 3) and Tests (xUnit).
- [ ] The start screen shows seven tiles, each with a large icon, a German name and a one-line description, all fitting on one screen without scrolling.
- [ ] Tiles for Tools not yet built are visibly unavailable, and clicking them does nothing confusing.
- [ ] Text is somewhat larger than the WinUI default, follows the Windows text-size setting, and the app follows the Windows light/dark setting.
- [ ] The window title is "PDF-Manager". All UI strings are German and kept in one resource file.
- [ ] A shared Tool screen frame provides a back button to the start screen, an *Öffnen* button, and a drop zone that accepts files dragged from Explorer.
- [ ] Dropping a PDF onto the *Zusammenfügen* tile opens Merge with that Document already loaded.
- [ ] A Merge session in Core loads Documents fully into memory, exposes the list of Documents in order, supports moving and removing Documents, reports whether saving is possible (at least two Documents), suggests an output name `<first Document> (zusammengefügt).pdf`, and saves to a given path.
- [ ] The merged Document contains every Page of each Document, in list order, with source rotations unchanged.
- [ ] The save dialog opens in the first Document's folder with the suggested name pre-filled, and a clear confirmation appears after saving.
- [ ] A source Document can be overwritten by the save, because sources are already in memory.
- [ ] Sample PDFs in the test project have a large unique label on every Page ("S1", "S2", …), and a test helper reads page labels, count and rotation from an output Document.
- [ ] Tests drive the Merge session the way the UI does (load, move, remove, save) and check the output's pages, order and rotation.

---

# 02: Friendly messages for files that can't be opened

**What to build:** When the parent opens a password-protected PDF, a damaged file, or something that isn't a PDF, the app explains in friendly German what's wrong and carries on normally. Nothing crashes, and the file is not added. This lives in the shared loading code, so every Tool built later gets it automatically.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Shared Document loading in Core reports Protected Document, damaged file and unsupported file as distinct, typed failures. It never makes a generic exception the UI has to interpret.
- [ ] A Protected Document shows a message saying the Document is password-protected and can't be edited. The app never tries to remove the password.
- [ ] A damaged file shows a message saying the file is damaged or can't be read.
- [ ] A non-PDF file shows a message saying only PDF files can be opened here.
- [ ] When several files are opened at once and one fails, the others still load, and the message names the failed file.
- [ ] Sample files (a Protected Document, a damaged PDF, a non-PDF) are in the test project, and tests check each failure type through the Merge session.

---

# 03: Undo/redo and the "Änderungen verwerfen?" prompt

**What to build:** In Merge the parent can undo and redo every change (adding, removing or moving a Document) with toolbar buttons or Ctrl+Z / Ctrl+Y. If they try to leave the Tool with unsaved changes, the app asks *"Änderungen verwerfen?"* first. After saving, the undo history is cleared. All later Tools reuse this behaviour.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Core sessions share undo/redo with can-undo/can-redo flags, and every user action in a session is undoable.
- [ ] Core sessions expose a has-unsaved-changes flag that becomes true after any change and false after saving.
- [ ] A successful save clears the undo/redo history.
- [ ] Undo and redo buttons are in the Tool screen frame, enabled only when possible, and Ctrl+Z / Ctrl+Y trigger them.
- [ ] Leaving a Tool through the back button or by closing the window with unsaved changes shows *"Änderungen verwerfen?"*. Cancelling stays in the Tool.
- [ ] Merge uses all of this.
- [ ] Tests on the Merge session cover undo, redo, redo being discarded after a new action, the unsaved-changes flag, and history being cleared by saving.

---

# 04: Edit pages (*Seiten bearbeiten*)

**What to build:** The parent opens one Document in *Seiten bearbeiten* and sees all its Pages as thumbnails with page numbers. Double-clicking a thumbnail opens a large Preview. They select Pages (with Ctrl/Shift-click for several), mark them for removal (greyed out with a red X, click again to unmark), Rotate them left or right, and Reorder them by dragging. Everything is undoable, and the saved Document matches exactly what's on screen. The page-rendering service and thumbnail grid built here also give Merge's cards a first-page thumbnail.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] A Core rendering service uses `Windows.Data.Pdf` to render a Page to an image at a requested size.
- [ ] A reusable thumbnail grid shows Pages with their page numbers, fills in thumbnails gradually without freezing the UI, and supports single, Ctrl and Shift selection.
- [ ] Double-clicking a thumbnail opens a Preview large enough to read, which closes by button, Escape or clicking outside it.
- [ ] An Edit pages session in Core supports marking/unmarking Pages for removal, rotating selected Pages by ±90° (stacking correctly), and moving Pages. All of these are undoable.
- [ ] Thumbnails immediately show removal marks and the new rotation.
- [ ] Saving is blocked, with an explanation, when every Page is marked for removal.
- [ ] The suggested name is `<Document> (bearbeitet).pdf`, and saving uses the shared save dialog.
- [ ] The saved Document has exactly the shown order, rotations, and no removed Pages.
- [ ] Merge cards show the first-page thumbnail of each Document.
- [ ] The *Seiten bearbeiten* tile works, including dropping a PDF onto it.
- [ ] Tests on the Edit pages session cover remove, unmark, rotate (including a sideways-scan sample), reorder, undo of each, the all-removed rule, and the saved output.

---

# 05: Blank page detection in Edit pages

**What to build:** When the parent opens a Document in *Seiten bearbeiten*, the app finds Blank pages (such as the empty backs of a duplex scan) and marks them for removal straight away. A banner says what happened, e.g. *"2 leere Seiten erkannt (Seite 4, 8) – werden beim Speichern entfernt"*. They can unmark any page the app got wrong. Detection is cautious: a Page with only a signature or a stamp is never marked.

**Blocked by:** 04

**Status:** ready-for-agent

- [ ] Core has Blank page detection that renders each Page at low resolution in grayscale and counts it as blank only if almost nothing is dark, allowing for scanner noise and dark scan edges.
- [ ] Detection runs only when a Document is loaded in Edit pages, and asynchronously so the UI never freezes.
- [ ] Detected Blank pages get the same removal mark as a manual removal and can be unmarked the same way.
- [ ] Unmarking a detected page can be undone. Initial detection is the starting state, not an undoable action.
- [ ] The banner lists the detected page numbers and appears only when at least one page was detected.
- [ ] A duplex-scan sample has blank backs (with simulated scanner noise) and one near-blank Page with only a signature, plus optionally one with only a stamp.
- [ ] Tests check that all blank backs are detected, the signature (and stamp) pages are not, and saving without changes drops exactly the detected pages.

---

# 06: Extract pages (*Seiten herauslösen*)

**What to build:** The parent opens a Document in *Seiten herauslösen*, selects the Pages they want, and saves them as a new Document. The extracted Pages keep their original order, and the original Document is unchanged.

**Blocked by:** 04

**Status:** ready-for-agent

- [ ] An Extract pages session in Core supports selecting and deselecting Pages, with each change undoable.
- [ ] Saving is possible only with at least one Page selected.
- [ ] The output contains exactly the selected Pages, in original order, with source rotations.
- [ ] The suggested name lists the Pages compactly, e.g. `Rechnung (Seiten 2-3).pdf` or `Rechnung (Seiten 1, 4-6).pdf`.
- [ ] Selected Pages are clearly highlighted in the thumbnail grid, and Preview works.
- [ ] No Blank page detection runs in this Tool.
- [ ] The tile works, including dropping a PDF onto it.
- [ ] Tests cover single, multiple and non-contiguous selections, the suggested name, undo, and the source file being unchanged.

---

# 07: Split (*Aufteilen*) and saving several files at once

**What to build:** The parent opens a Document in *Aufteilen* and clicks the gaps between thumbnails to place Split points (shown as scissors), clicking again to remove one. A coloured band under the thumbnails shows which Pages go into Teil 1, Teil 2, and so on. Alternatively they switch on *"Jede Seite einzeln"*. Saving asks once for a folder and base name, writes `<Basis> - Teil N.pdf`, and then offers *"Ordner öffnen"*. This ticket builds the shared multi-file saving that Page to image and the Compress fallback reuse.

**Blocked by:** 04

**Status:** ready-for-agent

- [ ] A Split session in Core supports adding/removing Split points between Pages and the "each Page separately" mode, all undoable, and exposes the resulting Parts as page ranges.
- [ ] Saving is possible only when the result has at least two Parts.
- [ ] Shared multi-file save in Core takes a folder and base name and returns the paths written.
- [ ] Shared multi-file save in the App shows one folder-and-base-name dialog (opened in the source folder, base name pre-filled), then a confirmation with an *"Ordner öffnen"* button that opens the folder in Explorer.
- [ ] Parts are named `<Basis> - Teil N.pdf`, numbered so they sort correctly in Explorer (zero-padded when there are 10 or more Parts).
- [ ] Each Part contains its Pages in order with source rotations.
- [ ] The coloured band updates immediately when Split points change.
- [ ] The tile works, including dropping a PDF onto it.
- [ ] Tests cover Split points at various positions, "each Page separately", undo, file naming, and the content of every Part.

---

# 08: Page to image (*Seite als Bild*)

**What to build:** The parent opens a Document in *Seite als Bild*, selects one or more Pages, and saves them as JPG images in a folder they choose. The images are named after the original page numbers. There are no format or resolution settings: the images are sharp enough to read and print.

**Blocked by:** 07

**Status:** ready-for-agent

- [ ] A Page to image session in Core supports selecting Pages (undoable), and saving is possible only with at least one Page selected.
- [ ] Each selected Page is rendered to a JPG at 200 dpi with the shared rendering service, including its rotation.
- [ ] Files are named `<Basis> - Seite N.jpg` with the original page number, and saved with the shared multi-file save, including *"Ordner öffnen"*.
- [ ] The tile works, including dropping a PDF onto it.
- [ ] Tests check the number of files, their names, that each is a valid JPG with the dimensions expected at 200 dpi, and that a rotated Page produces a rotated image.

---

# 09: Compress (*Verkleinern*) to the Email size target

**What to build:** The parent opens a Document in *Verkleinern* and presses one button. The app makes the Document smaller step by step until it is under 10 MB, the Email size target. It shows progress while working, then the result, such as *"Vorher 23,4 MB → Nachher 6,1 MB"*, and saves through the usual save dialog. Colours are never changed, text never becomes unreadably blurry, and text in digital Documents stays selectable. If the Document is already under 10 MB, the app says so and still offers a light compression.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Ghostscript (`gswin64c.exe` and its required files) is bundled with the app build, per ADR-0001.
- [ ] A Compress session in Core runs Ghostscript as a separate process into temporary files, trying 150, then 120, then 96 dpi image downsampling, and stops at the first result under 10 MB.
- [ ] It never goes below 96 dpi and never converts colour to grayscale or black-and-white.
- [ ] If the source is already under 10 MB, the session reports this and offers only the 150 dpi step.
- [ ] If even 96 dpi stays over 10 MB, the session reports that the target was not reached and keeps the 96 dpi result. The split-into-Parts offer comes in ticket 10.
- [ ] Original and resulting sizes are shown in German number formatting.
- [ ] Compress runs asynchronously with a progress indicator. The UI never freezes, and temporary files are cleaned up.
- [ ] The suggested name is `<Document> (verkleinert).pdf`, and saving uses the shared save dialog.
- [ ] The tile works, including dropping a PDF onto it.
- [ ] Tests use the real bundled Ghostscript and check that a large colour scan ends up under 10 MB, the digital invoice keeps selectable text, colour is kept, and the "already small enough" path works.

---

# 10: Compress fallback: split into Parts that fit an email

**What to build:** When a Document is still over 10 MB after the strongest Compress step, the app explains this and offers *"In mehrere E-Mail-taugliche Teile aufteilen"*. The parent chooses a folder and base name once, and gets several Parts that are each under 10 MB and can be sent in separate emails.

**Blocked by:** 07, 09

**Status:** ready-for-agent

- [ ] When the 96 dpi result is over target, the Compress session offers splitting into Parts.
- [ ] Parts are built from the 96 dpi result by adding Pages to the current Part until the next Page would take it over 10 MB, and every written Part is checked to be under target.
- [ ] A single Page that is over 10 MB on its own is reported clearly, not silently written over target.
- [ ] Parts are saved with the shared multi-file save as `<Basis> - Teil N.pdf`, with *"Ordner öffnen"* afterwards.
- [ ] A very large colour-scan sample that cannot reach 10 MB at 96 dpi exists, generated by a test helper or committed (implementer's choice, keeping the repository small).
- [ ] Tests check that the fallback is offered, every Part is under 10 MB, all Pages appear exactly once and in order across the Parts, and files are named correctly.

---

# 11: Images to PDF (*Bilder zu PDF*)

**What to build:** The parent adds JPG or PNG images, such as phone photos of letters, to *Bilder zu PDF*. They appear as thumbnails in the order added, and can be dragged into a different order or removed. Saving turns them into one Document with one A4 Page per image, the right way up, scaled to fit with a small margin. Landscape photos get landscape Pages. HEIC and other formats are rejected with a friendly message.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] An Images to PDF session in Core loads JPG and PNG files and supports moving and removing images, all undoable. Saving is possible with at least one image.
- [ ] Each image becomes one A4 Page. EXIF orientation is applied, page orientation matches the corrected aspect ratio, and the image is scaled to fit inside a small margin, centred, without distortion.
- [ ] Unsupported files (HEIC and anything not JPG/PNG) are reported as typed failures, and a friendly German message names the supported formats. Other files in the same drop still load.
- [ ] Images show as thumbnails, displayed the right way up.
- [ ] The suggested name is based on the first image's name, e.g. `<Bild>.pdf`, and saving uses the shared save dialog.
- [ ] The tile works, including dropping images onto it.
- [ ] Sample images: a JPG with EXIF rotation, a landscape JPG, a PNG, and a HEIC file.
- [ ] Tests check page count, A4 size and orientation per Page, order after reordering, removal, undo, and HEIC rejection.

---

# 12: Installer

**What to build:** The maintainer runs one `PDF-Manager-Setup.exe` on the parents' PC. It installs the self-contained app with Ghostscript, creates "PDF-Manager" shortcuts in the Start menu and on the desktop, and registers an uninstall entry. Installing a newer setup over the old one updates the app. Nothing else needs to be installed on the PC, and the app never goes online.

**Blocked by:** 01, 09

**Status:** ready-for-agent

- [ ] An Inno Setup script produces a single `PDF-Manager-Setup.exe` from a self-contained, unpackaged release build including the Windows App SDK runtime and Ghostscript.
- [ ] The installer is in German and creates Start-menu and desktop shortcuts named "PDF-Manager".
- [ ] Uninstalling through Windows settings removes the app and its shortcuts cleanly.
- [ ] Running a newer setup over an existing installation updates it in place, without asking to uninstall first and without leaving stale files behind.
- [ ] The app does not register any file associations.
- [ ] Building the installer is a single documented command.
- [ ] Manual check: on a clean Windows 11 machine or VM without the .NET or Windows App SDK runtimes, install, launch, run Compress, update, and uninstall all work.
