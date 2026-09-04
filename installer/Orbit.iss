; Inno Setup script for Orbit — builds publish\OrbitSetup-<version>.exe
; Compile with:  ISCC.exe installer\Orbit.iss
; (or run installer\build-installer.ps1 which publishes first)

#define AppName "Orbit"
#define AppVersion "1.7.1"
#define AppPublisher "Saxo"
#define AppExe "Orbit.exe"

[Setup]
AppId={{6F4C9A20-6D8B-4C2E-9E1E-0B7A2E9C1D42}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Let the user install anywhere; no admin required by default (per-user),
; but allow elevating to install for all users.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
AllowUNCPath=no
OutputDir=..\publish
OutputBaseFilename=OrbitSetup-{#AppVersion}
SetupIconFile=..\src\Orbit.App\Assets\orbit.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName} {#AppVersion}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0

[Languages]
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; The whole self-contained publish output (single-file Orbit.exe + any loose
; native helpers). PDBs are excluded.
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb,OrbitSetup-*.exe"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
