# Installer

`PDF-Manager-Setup.exe` installs PDF-Manager on the parents' PC (Windows 11). It holds everything the app needs (.NET and Windows App SDK runtimes, Ghostscript and the Visual C++ runtime it uses), so nothing else has to be installed and the app never goes online.

The setup hands Ghostscript (AGPL) to another PC. That is fine within the family; see [ADR-0001](../docs/adr/0001-ghostscript-for-compression.md) before ever giving it to anyone else.

## Building

Once, on the build PC: install the .NET 10 SDK and Inno Setup 6 (`winget install JRSoftware.InnoSetup`).

Then, from the repository folder:

```
powershell -ExecutionPolicy Bypass -File installer\build.ps1
```

The setup lands in `artifacts\PDF-Manager-Setup.exe`. Before handing out a new one, raise `<Version>` in `src\PdfManager.App\PdfManager.App.csproj`; the setup and the uninstall entry show it.

## What the setup does

- Installs to `C:\Program Files\PDF-Manager` (asks for administrator rights).
- Creates "PDF-Manager" shortcuts in the Start menu and on the desktop, and an uninstall entry in Windows settings.
- Updating: run a newer setup over the old installation. It closes a running PDF-Manager, replaces the whole app folder (no stale files) and keeps the shortcuts.
- Uninstalling while PDF-Manager is open: it asks to close the app first.
- Registers no file types: PDFs keep opening in whatever program they opened in before.

## Checking by hand before each install

The [spec](../docs/specs/0001-pdf-manager-v1.md) leaves the installer to a manual check. On a clean Windows 11 machine or VM without the .NET or Windows App SDK runtimes:

1. Run the setup; the app starts at the end.
2. Open a large scanned Document in *Verkleinern* (Compress) and compress it: this runs the bundled Ghostscript.
3. Raise the version, build again and run the new setup over the old one: no uninstall prompt, the new version shows in Windows settings.
4. Uninstall in Windows settings: `C:\Program Files\PDF-Manager` and both shortcuts are gone.
