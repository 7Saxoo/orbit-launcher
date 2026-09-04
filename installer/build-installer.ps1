<#
    Publishes Orbit as a self-contained single file, then compiles the
    Windows installer (publish\OrbitSetup-<version>.exe).

    Usage:  pwsh installer\build-installer.ps1
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

Write-Host "==> Publishing (self-contained, single file)…" -ForegroundColor Cyan
dotnet publish src/Orbit.App/Orbit.App.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -o publish --nologo

# Locate ISCC (Inno Setup command-line compiler)
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup not found. Install it with:  winget install JRSoftware.InnoSetup"
}

Write-Host "==> Compiling installer with $iscc" -ForegroundColor Cyan
& $iscc "installer\Orbit.iss"

Get-ChildItem "publish\OrbitSetup-*.exe" | ForEach-Object {
    Write-Host ("==> {0}  ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB)) -ForegroundColor Green
}
