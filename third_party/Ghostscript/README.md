# Ghostscript

GPL Ghostscript 10.06.0 (2025-09-09), 64-bit Windows, used by Compress only and run as a separate process ([ADR-0001](../../docs/adr/0001-ghostscript-for-compression.md)).

- `gswin64c.exe` and `gsdll64.dll` are copied unchanged from the official installer's `bin` folder (`gs10060w64.exe` from <https://ghostscript.com/releases/gsdnld.html>). The initialisation files, fonts and colour profiles are compiled into the DLL, so nothing else is needed.
- `COPYING` is the licence (GNU AGPL 3.0). It is fine for family use only. If the app is ever published, revisit ADR-0001.

The build copies these files into a `Ghostscript` folder next to the app and next to the tests.

To update: install the new version, copy the two files from its `bin` folder and its `doc\COPYING` here, and run the Compress tests.
