# Office Buddy v1

## Problem Statement

My parents regularly get PDFs they need to reshape before passing them on: scans from their home scanner, and attachments from emails such as invoices and letters. Again and again they need to combine several Documents into one, take out pages (especially the empty backs of duplex scans), turn sideways pages the right way up, or cut a Document into pieces. Most often, a scanned Document is too big to email: the email bounces and they don't know what to do.

The tools they could use today are either online services (bills, medical letters and tax documents should not be uploaded anywhere), cluttered professional software with a menu for everything, or paid. None of them is in German, simple, and offline at the same time.

## Solution

A German-language Windows 11 desktop app, **Office Buddy**, that works entirely on their computer. It opens on a clean start screen with seven large tiles, one per Tool:

| Tile | Tool |
|---|---|
| *Zusammenfügen* | Merge |
| *Seiten bearbeiten* | Edit pages (Remove pages, Rotate, Reorder, with automatic Blank page detection) |
| *Seiten herauslösen* | Extract pages |
| *Aufteilen* | Split |
| *Verkleinern* | Compress |
| *Bilder zu PDF* | Images to PDF |
| *PDF als Bild* | Page to image |

Each Tool is its own screen. It goes from opening a Document, through choosing what to do with clickable page thumbnails, to saving the result with a pre-filled save dialog. Everything can be undone until the result is saved. Compress aims for the Email size target (10 MB) so the result can be sent almost anywhere. If a Document cannot get that small, the app offers to split it into Parts that each fit.

## User Stories

### Start screen and navigation

1. As a parent, I want the app to open on a start screen showing all Tools as large tiles, so that I can see at a glance what the app can do.
2. As a parent, I want each tile to have a large icon, a name and a one-line description in German, so that I know which tile to choose without guessing.
3. As a parent, I want all seven tiles to fit on one screen without scrolling, so that I never miss a Tool.
4. As a parent, I want to drop a PDF onto a tile, so that the Tool opens with my Document already loaded.
5. As a parent, I want a clearly visible way back from a Tool to the start screen, so that I never feel stuck.
6. As a parent, I want to be asked *"Änderungen verwerfen?"* when I leave a Tool with unsaved changes, so that I don't lose work by accident.
7. As a parent, I want the app's text to be somewhat larger than standard Windows text, so that I can read it comfortably.
8. As a parent, I want the app to follow my Windows text-size setting, so that it matches the rest of my computer.
9. As a parent, I want the app to follow my Windows light/dark setting, so that it looks like it belongs on my computer.
10. As a parent, I want every label, button, message and dialog in German, so that I understand everything.

### Opening Documents

11. As a parent, I want to open Documents with a big *Öffnen* button, so that I can find my files the usual Windows way.
12. As a parent, I want to drag PDFs from File Explorer into a Tool screen, so that I don't have to go through a dialog.
13. As a parent, I want a friendly message saying the Document is protected when I open a Protected Document, so that I understand why nothing happens and don't think the app is broken.
14. As a parent, I want a friendly message when a file is damaged or is not a PDF, so that I know the problem lies with the file.
15. As a parent, I want large Documents to show their thumbnails progressively without the app freezing, so that I can start working straight away.

### Thumbnails and Preview

16. As a parent, I want to see every Page as a thumbnail with its page number, so that I can recognise pages.
17. As a parent, I want to double-click a thumbnail to see a large Preview of that Page, so that I can check what is on it before deciding.
18. As a parent, I want to close the Preview easily (button, Escape, or clicking outside it), so that I get straight back to the thumbnails.
19. As a parent, I want selected thumbnails to be clearly highlighted, so that I always know which Pages an action will apply to.
20. As a parent, I want to select several Pages with Ctrl-click and Shift-click, so that I can work on many Pages at once.

### Undo/redo

21. As a parent, I want undo and redo buttons in every Tool, so that I can take back a mistake.
22. As a parent, I want Ctrl+Z and Ctrl+Y to work, so that I can undo quickly.
23. As a parent, I want the undo history to cover everything I did in the Tool since opening it, so that I can step back as far as I need.
24. As a parent, I want the undo history to be cleared after saving, so that it's clear the saved result is final.

### Saving

25. As a parent, I want a save dialog every time I save a single result Document, so that I decide where it goes and what it is called.
26. As a parent, I want the save dialog to open in the folder of the Document I started with, so that the result ends up next to the original.
27. As a parent, I want the save dialog to suggest a sensible name, such as `Rechnung (verkleinert).pdf`, so that I usually only have to press Enter.
28. As a parent, I want Windows to warn me before overwriting an existing file, so that I don't destroy something by accident.
29. As a parent, I want to be able to overwrite the original Document if I deliberately choose to, so that I don't collect copies I don't need.
30. As a parent, I want Tools that produce several files to ask once for a folder and a base name, so that I don't have to click through a dialog for every file.
31. As a parent, I want those files to be named automatically, such as `Kontoauszug - Teil 1.pdf` or `Kontoauszug - Seite 3.jpg`, so that they are easy to tell apart and sort correctly.
32. As a parent, I want an *"Im Ordner zeigen"* button after every save, so that I can see my new file or files in Explorer right away, for example to drag them into an email.
33. As a parent, I want a clear confirmation after saving, so that I know it worked.

### Merge (*Zusammenfügen*)

34. As a parent, I want to add two or more Documents to Merge, so that they become one Document.
35. As a parent, I want each added Document shown as a card with the thumbnail of its first page, its name and its page count, so that I can tell them apart.
36. As a parent, I want to drag the cards into the order I want, so that the merged Document has its pages in the right sequence.
37. As a parent, I want to remove a card I added by mistake, so that it isn't merged.
38. As a parent, I want to add more Documents after the first ones, so that I don't have to start again.
39. As a parent, I want the save button to be available only when at least two Documents are added, so that I can't produce a pointless result.
40. As a parent, I want the merged Document to contain all Pages of each Document, in card order and unchanged, so that nothing is lost.

### Edit pages (*Seiten bearbeiten*)

41. As a parent, I want to open one Document and see all its Pages, so that I can tidy it up in one place.
42. As a parent, I want to mark Pages for removal, shown greyed out with a red X, so that I can see what will be left out before saving.
43. As a parent, I want to unmark a Page by clicking it again, so that I can change my mind.
44. As a parent, I want to Rotate selected Pages left or right by 90°, so that sideways scans read correctly.
45. As a parent, I want the thumbnail to show the new rotation immediately, so that I can see the result.
46. As a parent, I want to Reorder Pages by dragging thumbnails, so that the Document reads in the right order.
47. As a parent, I want Blank pages to be detected automatically when I open a Document here, so that I don't have to hunt for empty scan backs.
48. As a parent, I want detected Blank pages to be marked for removal but not removed until I save, so that I can check them first.
49. As a parent, I want a banner such as *"2 leere Seiten erkannt (Seite 4, 8) – werden beim Speichern entfernt"*, so that I know the app did this on its own.
50. As a parent, I want to unmark a Page the app wrongly detected as blank, so that a page with only a signature or a stamp is kept.
51. As a parent, I want Blank page detection to be cautious, so that pages with faint content are not marked.
52. As a parent, I want saving to be blocked with an explanation if all Pages are marked for removal, so that I can't create an empty Document.
53. As a parent, I want the saved Document to reflect exactly the order, rotation and removals I see, so that there are no surprises.

### Extract pages (*Seiten herauslösen*)

54. As a parent, I want to select Pages from a Document and save them as a new Document, so that I can pass on only the relevant part.
55. As a parent, I want the original Document to remain unchanged, so that I keep the complete version.
56. As a parent, I want the extracted Pages to keep their original order, so that the result reads naturally.
57. As a parent, I want the suggested name to show which Pages were extracted, such as `Rechnung (Seiten 2-3).pdf`, so that I recognise it later.

### Split (*Aufteilen*)

58. As a parent, I want to click the gap between two thumbnails to place a Split point (scissors marker), so that I decide where one Part ends and the next begins.
59. As a parent, I want to click a Split point again to remove it, so that I can correct mistakes.
60. As a parent, I want a coloured band under the thumbnails showing which Pages go into Teil 1, Teil 2 and so on, so that I can see the result before saving.
61. As a parent, I want a switch *"Jede Seite einzeln"*, so that every Page becomes its own Document without placing Split points myself.
62. As a parent, I want to save all Parts in one go into a folder I choose, named `<Basis> - Teil N.pdf`, so that splitting is quick.

### Compress (*Verkleinern*)

63. As a parent, I want to open a Document and press one button to make it small enough for email, so that my email no longer bounces.
64. As a parent, I want to see the size before and after, such as *"Vorher 23,4 MB → Nachher 6,1 MB"*, so that I know it worked.
65. As a parent, I want the app to keep trying stronger settings automatically until the Document is under the Email size target, so that I don't have to understand quality settings.
66. As a parent, I want the app never to make text unreadably blurry, so that the recipient can still read the Document.
67. As a parent, I want colours to be kept, so that coloured stamps, logos and photos still look right.
68. As a parent, I want text in digital Documents to remain selectable after Compress, so that the recipient can still search and copy it.
69. As a parent, I want to be told when the Document cannot be made small enough, and offered *"In mehrere E-Mail-taugliche Teile aufteilen"*, so that I can still send it as several emails.
70. As a parent, I want each of those Parts to be under the Email size target, so that every email goes through.
71. As a parent, I want to be told if my Document is already under the Email size target, and still be offered a light compression, so that I don't do unnecessary work but can make it smaller if I want.
72. As a parent, I want a progress indicator while Compress runs, so that I know the app hasn't frozen.

### Images to PDF (*Bilder zu PDF*)

73. As a parent, I want to add JPG and PNG images, for example phone photos of letters, so that they become one Document.
74. As a parent, I want each image to become one A4 Page, scaled to fit with a small margin, so that the result looks like a normal document.
75. As a parent, I want photos taken with a turned phone to appear the right way up automatically, so that I don't have to rotate them.
76. As a parent, I want landscape images to get a landscape Page, so that they are not shrunk unnecessarily.
77. As a parent, I want the images in the order I added them, and to be able to Reorder them by dragging, so that the pages are in the right sequence.
78. As a parent, I want to remove an image I added by mistake, so that it isn't included.
79. As a parent, I want a friendly message when I add an unsupported file, such as HEIC, so that I know which formats work.

### Page to image (*PDF als Bild*)

80. As a parent, I want to select one or more Pages and save them as JPG images, so that I can send or use a page as a picture.
81. As a parent, I want the images to be sharp enough to read and print without choosing any settings, so that it just works.
82. As a parent, I want the images named `<Basis> - Seite N.jpg` with the original page number, so that I know which page each image shows.

### Installation and maintenance

83. As the maintainer, I want to install the app with a single setup file that needs nothing else on the computer, so that installing during a visit or remote session is quick.
84. As the maintainer, I want the setup to create a Start-menu and desktop shortcut called "Office Buddy", so that my parents find the app easily.
85. As the maintainer, I want to update by running a newer setup over the old installation, so that updating is just as simple.
86. As the maintainer, I want an uninstall entry in Windows settings, so that the app can be removed cleanly.
87. As a parent, I want the app never to send anything over the internet, so that my private Documents stay on my computer.
88. As a parent, I want the app not to take over as my default PDF viewer, so that opening PDFs works as before.

## Implementation Decisions

### Platform and delivery
- Windows 11 only, .NET 10, WinUI 3 (Windows App SDK), built as an **unpackaged, self-contained** app so the .NET runtime and Windows App SDK ship with it.
- Delivered as one self-contained `OfficeBuddy.exe` that runs without installing; shortcuts are made by hand. Updates replace the exe. No installer, no auto-updater, no MSIX, no Store. (Originally an Inno Setup installer with shortcuts and an uninstall entry; dropped in favour of the single exe.)
- Fully offline: no network access of any kind.
- German-only UI, no localisation layer beyond keeping strings in one resource file. Base font size somewhat larger than WinUI's default. Respects the Windows text scaling and theme settings.
- No file association, no "Open with" registration, no Explorer context-menu entries. Documents are opened only through the *Öffnen* button, by dragging into a Tool screen, or by dropping onto a start-screen tile.

### Architecture
- **`OfficeBuddy.Core`** holds all PDF logic and all Tool state, with no UI dependencies. It targets the Windows-specific .NET framework so it can use `Windows.Data.Pdf`.
- **`OfficeBuddy.App`** is the WinUI 3 app: start screen, one screen per Tool, dialogs. It stays thin. Screens display a Core session and forward user actions to it; they contain no PDF or state logic of their own.
- **`OfficeBuddy.Core.Tests`** holds xUnit tests against Core.
- Seven separate Tool screens per ADR-0002, with Edit pages combining Remove pages, Rotate and Reorder.

### Tool sessions (the Core interface)
- Each Tool has a Core **session** type: Merge, Edit pages, Extract pages, Split, Compress, Images to PDF, Page to image. A session is the complete state of one Tool screen.
- Sessions describe the planned result. Source Documents are read fully into memory when loaded, so the source file can be overwritten on save, and nothing is written until save.
- Common session behaviour:
  - load one or more Documents (or images, for Images to PDF), reporting Protected Document and damaged/unsupported file as distinct, typed failures that the UI turns into German messages
  - expose the current page or card list for display, with each entry referring to its source Document and source page index plus any pending rotation or marks
  - user actions as methods (select, mark/unmark, rotate, move, add/remove Split point, add/remove/move card)
  - undo/redo with can-undo/can-redo flags; every user action is undoable; history cleared after a successful save
  - a has-unsaved-changes flag, used for the *"Änderungen verwerfen?"* prompt
  - a suggested output name based on the first source Document and the Tool, e.g. `(zusammengefügt)`, `(bearbeitet)`, `(Seiten 2-3)`, `(verkleinert)`
  - save to a single file path (Merge, Edit pages, Extract pages, Compress, Images to PDF), or to a folder plus base name (Split, Page to image, Compress fallback into Parts), returning the paths written
  - a can-save flag covering rules like "at least two Documents for Merge" and "not every Page removed"
- Long-running work (loading, Blank page detection, Compress, saving) is asynchronous and reports progress so the UI never freezes.

### PDF libraries
- **PDFsharp 6.2.x (MIT)** for building output Documents: Merge, Remove pages, Rotate, Reorder, Extract pages, Split, Images to PDF. Stay on the 6.2 line: from 6.5 on, PDFsharp is AGPL-3.0 with a runtime license check.
- **`Windows.Data.Pdf`** (built into Windows) for rendering: thumbnails, Preview, Page to image, and the rasterisation behind Blank page detection.
- **Ghostscript** (`gswin64c.exe`), bundled next to the app and invoked as a separate process, for Compress only. See ADR-0001, including the AGPL note.

### Tool-specific behaviour
- **Merge:** whole Documents only, no page selection. Output is all Pages of each Document, in card order, rotations as in the source.
- **Edit pages:** on load, Blank page detection runs, and every detected Blank page is marked for removal (the same mark as a manual removal, so the user can unmark it). The banner lists the detected page numbers. Blank page detection runs only in this Tool.
- **Blank page detection:** render each Page at low resolution in grayscale and treat it as blank only if almost no pixels are dark, with tolerance for scanner noise and dark scanner edges. The threshold is tuned on the sample Documents to be conservative: a Page with only a signature or a stamp must not count as blank.
- **Extract pages:** output contains the selected Pages in their original order. The source is not modified.
- **Split:** either Split points between Pages, or "each Page separately". Output files are `<Basis> - Teil N.pdf`.
- **Compress:**
  - Step through Ghostscript runs that downsample images to 150, then 120, then 96 dpi, stopping at the first result under the Email size target (10 MB). Never go below 96 dpi. Never convert colour to grayscale or black-and-white.
  - Report the original and resulting size.
  - If the source is already under target, say so and offer only the 150 dpi step.
  - If 96 dpi is still over target, offer to split the 96 dpi result into Parts by accumulating Pages until the next Page would push a Part over the target, then verify each written Part is under target.
  - Each Ghostscript run writes to a temporary file. Only the final result is saved to the user's chosen location.
- **Images to PDF:** JPG and PNG only. One image per A4 Page, orientation matching the image's aspect ratio after applying its EXIF rotation, scaled to fit with a small margin. Images keep the order they were added, can be reordered and removed. HEIC and other formats are rejected with a friendly message.
- **Page to image:** selected Pages rendered to JPG at 200 dpi, named `<Basis> - Seite N.jpg` with the source page number. No format or resolution options.

### Saving and dialogs
- Single-file Tools show a save dialog every time, opened in the first source Document's folder, pre-filled with the suggested name. The Windows overwrite warning is the only overwrite protection.
- After every save, the success message offers *"Im Ordner zeigen"*, which opens File Explorer with the saved file (the first one, for several) selected.
- Multi-file Tools show one dialog for a folder and base name.

### UI conventions
- Thumbnails with page numbers. Double-click opens the Preview, which closes by button, Escape or clicking outside it. Ctrl/Shift-click for multi-select.
- Undo/redo as toolbar buttons and Ctrl+Z / Ctrl+Y.
- Leaving a Tool with unsaved changes prompts *"Änderungen verwerfen?"*.

## Testing Decisions

- **One test seam: the Tool sessions in `OfficeBuddy.Core`.** Tests act like a user, through exactly the calls the UI makes: load, mark, rotate, move, undo, save. Then they assert on the Documents written to disk. They never reach into session internals.
- **A good test describes behaviour the parent would notice.** Examples: "removing page 2 and rotating page 3, then undoing the rotation, saves a 3-page Document with pages S1, S3, S4, none rotated", or "a 30 MB colour scan compresses to under 10 MB". Tests must not break if the internal page model or Ghostscript arguments change, as long as the outcome is the same.
- **Sample Documents** are kept in the test project. Every Page carries a large unique label (e.g. "S3"), so tests can check page identity and order by reading text from the output. Rotation and page count are read with PDFsharp. The set:
  - a digital invoice with selectable text, which must stay selectable after Compress
  - a duplex scan with Blank pages on the backs, plus one near-blank Page with only a signature, which must not be detected
  - a scan with a sideways Page
  - a large colour scan that compresses under 10 MB
  - a very large colour scan that cannot reach 10 MB at 96 dpi, to exercise the split-into-Parts fallback
  - a Protected Document
  - a damaged file
  - JPG images with EXIF rotation, a landscape image, a PNG, and a HEIC file to check it is rejected
- **Compress tests use the real bundled Ghostscript**, not a fake.
- **Not tested automatically:** WinUI screens, file dialogs, drag and drop, thumbnail appearance, the installer. These are checked by hand before each install.
- **No prior art:** this is a new codebase, and these tests set the pattern.

## Out of Scope

- Printing (parents print from Edge or Acrobat)
- OCR, editing text, filling forms, comments and annotations, blacking out text
- Adding, removing or changing passwords, and working on Protected Documents in any way
- Signatures, page numbers, text stamps, watermarks
- Converting to grayscale or black-and-white, straightening crooked scans
- Sending email from the app
- HEIC and any image format other than JPG/PNG
- Page selection during Merge
- Compress quality levels or other user-facing compression settings
- Format or resolution options for Page to image
- File associations, "Open with" registration, Explorer context-menu entries
- Recently used files
- Automatic updates and any network access
- Languages other than German, operating systems other than Windows 11

## Further Notes

- Vocabulary follows [GLOSSARY.md](../../GLOSSARY.md). Code and docs use the English terms. The UI uses the German terms listed there.
- [ADR-0001](../adr/0001-ghostscript-for-compression.md): Ghostscript for Compress. Its AGPL licence is fine for family use but must be revisited if the app is ever published.
- [ADR-0002](../adr/0002-one-tool-per-tile.md): one Tool per tile, with Edit pages as the combined exception.
- The very large colour scan sample may be generated by a test helper instead of committed, to keep the repository small. This is the implementer's choice.
- The project is not under version control yet. The maintainer will add git later.
