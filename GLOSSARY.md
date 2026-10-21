# Office Buddy

A German-language Windows desktop tool that lets non-expert users reshape PDF files they received by email or produced with a scanner, and resize or convert photos and images, entirely on their own computer.

## Language

**Tool**:
One self-contained job the user starts from a tile on the start screen, with its own screen from opening Documents to saving the result. UI term: *Werkzeug*, though the tiles show only the action name.
_Avoid_: Feature, task, card, function

**Document**:
A single PDF file on disk that the user opens or saves. UI term: *Dokument*.
_Avoid_: File, PDF (when referring to the thing being worked on)

**Page**:
One page of a Document, shown to the user as a clickable thumbnail. UI term: *Seite*.
_Avoid_: Sheet, slide

**Preview**:
A single Page shown large enough to read, opened by double-clicking its thumbnail. UI term: *Vorschau*.
_Avoid_: Zoom, viewer, detail view

**Blank page**:
A Page with essentially no ink apart from scanner noise, typically the empty back of a duplex scan. A page with only a signature or stamp is not blank. UI term: *leere Seite*.
_Avoid_: Empty page, white page

**Edit pages**:
The Tool that does Remove pages, Rotate and Reorder on one Document in one thumbnail view. UI term: *Seiten bearbeiten*.
_Avoid_: Page editor, organize

**Merge**:
Combining two or more whole Documents into one new Document, in an order the user chooses. Individual Pages cannot be chosen while merging. UI term: *Zusammenfügen*.
_Avoid_: Combine, join, concatenate

**Remove pages**:
Leaving selected Pages out of the Document that gets saved. UI term: *Seiten entfernen*.
_Avoid_: Delete pages, cut

**Compress**:
Making a Document's file smaller until it reaches the Email size target, accepting lower image quality. UI term: *Verkleinern*.
_Avoid_: Optimize, shrink, reduce

**Email size target**:
The file size a compressed Document must get under so it can be sent as an email attachment almost anywhere. Set at 10 MB.
_Avoid_: Size limit, max size, quota

**Rotate**:
Turning selected Pages by 90° steps. UI term: *Drehen*.
_Avoid_: Turn, flip

**Reorder**:
Changing the order of Pages within one Document by dragging thumbnails. UI term: *Sortieren*.
_Avoid_: Sort, rearrange, move

**Extract pages**:
Saving selected Pages as a new Document while the original stays unchanged. UI term: *Seiten herauslösen*.
_Avoid_: Copy pages, export pages

**Split**:
Cutting one Document into several Documents, either one per Page or at split points the user chooses. UI term: *Aufteilen*.
_Avoid_: Separate, burst, divide

**Split point**:
A cut the user places between two Pages, where one Part ends and the next begins. UI term: *Trennstelle*.
_Avoid_: Cut, marker, break

**Part**:
One of the Documents that results from a Split. UI term: *Teil*.
_Avoid_: Chunk, piece, section

**Images to PDF**:
Turning one or more photos or image files into a single Document, one image per Page. UI term: *Bilder zu PDF*.
_Avoid_: Convert, import images

**Page to image**:
Saving one or more Pages as image files. UI term: *PDF als Bild*.
_Avoid_: Export, render, screenshot

**Resize image**:
Saving one JPG or PNG image at a different size in pixels, given either as a percentage or as a width and height, keeping its proportions unless the user turns that off. The result keeps the image's format and is written the right way up. UI term: *Bildgröße ändern*.
_Avoid_: Scale, shrink image, compress image

**Protected Document**:
A Document that needs a password to open. The app refuses to work on it and does not try to remove the password.
_Avoid_: Encrypted PDF, locked file
