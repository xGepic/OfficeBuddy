# Ghostscript

GPL Ghostscript 10.06.0 (2025-09-09), 64-bit Windows, used by Compress, PDF-Passwort (to take off a password with only the one to open the file), PDF reparieren and Unterschreiben (for a PDF whose owner forbids changes), and run as a separate process ([ADR-0001](../../docs/adr/0001-ghostscript-for-compression.md)).

- `gswin64c.exe` and `gsdll64.dll` are copied unchanged from the official installer's `bin` folder (`gs10060w64.exe` from <https://ghostscript.com/releases/gsdnld.html>). The initialisation files, fonts and colour profiles are compiled into the DLL, so nothing else is needed.
- `vcruntime140.dll`, `vcruntime140_1.dll` and `msvcp140.dll` are the Visual C++ runtime (14.51, from the installed Visual C++ Redistributable) that Ghostscript needs. A clean Windows 11 doesn't necessarily have them, so they sit next to `gswin64c.exe`, as Microsoft allows for app-local deployment.
- `COPYING` is the licence (GNU AGPL 3.0), which Office Buddy shares ([ADR-0003](../../docs/adr/0003-publish-under-agpl.md)).
- The source of exactly this version, as the AGPL requires: <https://github.com/ArtifexSoftware/ghostpdl-downloads/releases/tag/gs10060> (`ghostscript-10.06.0.tar.gz`). The release notes of every build link to it too.

The build copies these files into a `Ghostscript` folder next to the app and next to the tests.

To update: install the new version, copy the two files from its `bin` folder and its `doc\COPYING` here, update the source link above and in [build-exe.yml](../../.github/workflows/build-exe.yml), and run the Compress tests. If a newer Ghostscript needs other runtime files, `strings -a gsdll64.dll | grep -i "\.dll$"` lists what it loads.
