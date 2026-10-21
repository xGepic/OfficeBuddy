# Builds artifacts\PDF-Manager-Setup.exe: publishes a self-contained release build of the app,
# then compiles installer\PDF-Manager.iss with Inno Setup 6.
#
#   powershell -ExecutionPolicy Bypass -File installer\build.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $root 'artifacts\publish'

$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) {
    throw 'Inno Setup 6 is not installed: winget install JRSoftware.InnoSetup'
}

# A clean folder, so the setup holds exactly this build.
if (Test-Path $publish) {
    Remove-Item $publish -Recurse -Force
}
dotnet publish (Join-Path $root 'src\PdfManager.App') -c Release -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

& $iscc (Join-Path $PSScriptRoot 'PDF-Manager.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }

Write-Host "Done: $(Join-Path $root 'artifacts\PDF-Manager-Setup.exe')"
