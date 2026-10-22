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
The Tool that does Remove pages, Rotate, Reorder and Insert pages on one Document in one thumbnail view. UI term: *Seiten bearbeiten*.
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
The file size a compressed Document must get under so it can be sent as an email attachment. 10 MB, which gets through almost anywhere, unless the user sets 20 or 25 MB in the settings for their email provider.
_Avoid_: Size limit, max size, quota

**Rotate**:
Turning selected Pages by 90° steps. UI term: *Drehen*.
_Avoid_: Turn, flip

**Reorder**:
Changing the order of Pages within one Document by dragging thumbnails. UI term: *Sortieren*.
_Avoid_: Sort, rearrange, move

**Insert pages**:
Adding all Pages of one or more other Documents into the Document in Edit pages, after the last selected Page or at the end. They can then be rotated, reordered and removed like its own Pages, and are saved with it. Unlike Merge, it works inside one Document at a chosen place. UI term: *Seiten einfügen*.
_Avoid_: Add pages, import pages, append

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

**Picture in picture**:
Placing one image, the Inserted image, on top of another, the Main image, and saving the result as one image at the Main image's size and in its format. The Inserted image always lies whole on the Main image; where it is transparent, the Main image shows through. UI term: *Bild in Bild*.
_Avoid_: Overlay, watermark, stamp

**Main image**:
The photo or image in Picture in picture that another image is placed on. UI term: *Foto* or *Bild*.
_Avoid_: Background, base image

**Inserted image**:
The image placed on top of the Main image, such as a logo or a signature. UI term: *eingefügtes Bild*.
_Avoid_: Overlay, sticker

**Images side by side**:
Saving two or more images as one, lined up in a row or a column with a thin white gap between them, all at the height (in a row) or width (in a column) of the smallest. UI term: *Bilder nebeneinander*.
_Avoid_: Collage, merge images, combine images

**Crop image**:
Saving only a chosen rectangle of one JPG or PNG image, optionally turned in 90° steps first, at its full size in pixels. The result keeps the image's format and is written the right way up. UI term: *Bild zuschneiden*.
_Avoid_: Trim, cut, clip

**Favorite**:
A Tool the user has marked in the settings so that its tile shows first on the start screen, under its own heading, instead of in its group. UI term: *Favorit*.
_Avoid_: Pinned, starred, bookmark

**Save location**:
The folder the save dialog opens in, chosen in the settings: next to the original (the default), the Documents folder, the Desktop, or a folder of the user's own. The user can still pick any folder in the dialog. UI term: *Speichern unter*.
_Avoid_: Output folder, default directory, target path

**Sign**:
Putting a picture of the user's own signature onto one or more Pages of a Document, each placed and sized on its Page, and saving the result. The signature's white paper becomes transparent. It is a picture, not a legally binding electronic signature. UI term: *Unterschreiben*.
_Avoid_: E-signature, digital signature, stamp

**Signature**:
The JPG or PNG picture of the user's signature used by Sign, usually a phone photo or scan of it on white paper. Remembered for the next time. UI term: *Unterschrift*.
_Avoid_: Autograph, initials

**Recognize text**:
Reading the text on a Document's Pages, usually a scan, with Windows' text recognition, to copy it or to save the Document searchable: the text lies invisibly over each Page where it was seen, so the Document looks the same. UI term: *Text erkennen*.
_Avoid_: OCR (in the UI), convert to text, extract text

**PDF password**:
The Tool that saves a Protected Document without its password, once the user has typed it, or gives any Document a new password. It never tries to find a password out. UI term: *PDF-Passwort*.
_Avoid_: Unlock, crack, encrypt, decrypt

**Repair**:
Rebuilding a damaged Document, such as a broken-off download, into one that opens again, with whatever Pages could be saved. A Document that isn't damaged needs no repair. UI term: *PDF reparieren*.
_Avoid_: Fix, recover, restore

**Tool group**:
The heading a Tool's tile is shown under on the start screen, by what it works on: putting PDF Pages in order (*PDF-Seiten ordnen*), changing a whole PDF (*PDF-Dateien bearbeiten*), from pictures to PDF and back (*PDF und Bilder umwandeln*), and photos (*Fotos und Bilder*).
_Avoid_: Category, section, tab

**Protected Document**:
A Document that needs a password to open. Every Tool but PDF password refuses it; PDF password opens it only with its password, and the app never tries to find the password out.
_Avoid_: Encrypted PDF, locked file
