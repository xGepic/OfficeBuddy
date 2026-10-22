# Bundle Ghostscript for Compress

Compress has to re-encode the images inside scanned Documents, and no free .NET library does that well. We ship Ghostscript (`gswin64c.exe`) next to the app and call it as a separate process for Compress only. All other operations use PDFsharp and `Windows.Data.Pdf`.

## Considered Options

- **Rasterize each Page to JPEG with `Windows.Data.Pdf` and rebuild the PDF.** No extra dependency, but text stops being selectable and digital (non-scanned) Documents get larger and blurrier.
- **Re-encode embedded images ourselves on top of PDFsharp.** Clean MIT licensing, but weeks of work across many image encodings and colour spaces.

## Consequences

Ghostscript is AGPL. That is fine while the app is used only within the family and never distributed publicly. If the app is ever published or sold, this decision must be revisited, most likely in favour of re-encoding images on top of PDFsharp.

Revisited when the repository went public: Ghostscript stays, and Office Buddy is AGPL too ([ADR-0003](0003-publish-under-agpl.md)).
