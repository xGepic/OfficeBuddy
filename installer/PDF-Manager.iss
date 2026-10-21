; Installer for PDF-Manager: one PDF-Manager-Setup.exe that installs the self-contained app
; (.NET and Windows App SDK runtimes included) with Ghostscript. Build it with installer\build.ps1.

#define AppName "PDF-Manager"
#define AppExe "PdfManager.exe"
#define PublishDir "..\artifacts\publish"
#define AppVersion GetVersionNumbersString(PublishDir + "\" + AppExe)
; Created by the app while it runs (App.xaml.cs).
#define AppMutex "PdfManager.Running"

[Setup]
; Never change the AppId: a newer setup finds the installed app by it and updates it in place.
AppId={{D7B56D54-7D48-477A-A34D-4C4794D2CD4E}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
DefaultDirName={autopf}\{#AppName}
; Always the same folder, so an update replaces exactly what was installed.
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
ShowLanguageDialog=no
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 11, as the spec says.
MinVersion=10.0.22000
PrivilegesRequired=admin
; On an update, a running PDF-Manager is closed before its files are replaced.
CloseApplications=yes
RestartApplications=no
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=..\artifacts
OutputBaseFilename=PDF-Manager-Setup
SetupIconFile=..\src\PdfManager.App\Assets\PdfManager.ico
Compression=lzma2/max
SolidCompression=yes

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[InstallDelete]
; An update replaces the whole app, so no file of an older version is left behind.
Type: filesandordirs; Name: "{app}\*"

[UninstallDelete]
; After an update the folder counts as already there, so Inno would leave it behind empty.
Type: dirifempty; Name: "{app}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{#AppName} jetzt starten"; Flags: nowait postinstall skipifsilent

[Code]
// The uninstaller can't remove the files of a running PDF-Manager, so it asks to close it first.
// Silent, it gives up instead of leaving half an installation behind.
function InitializeUninstall(): Boolean;
begin
  Result := True;
  while Result and CheckForMutexes('{#AppMutex}') do
    Result := SuppressibleMsgBox('PDF-Manager ist noch geöffnet. Bitte schließen Sie es und klicken Sie dann auf OK.',
      mbError, MB_OKCANCEL, IDCANCEL) = IDOK;
end;
