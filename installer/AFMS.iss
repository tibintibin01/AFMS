#define MyAppName "AFMS Inventory"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Municipal Treasury Office"
#define MyAppExeName "AFMS.App.exe"

[Setup]
AppId={{90B93627-AEC3-43B2-93A9-D6926A0B0644}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\AFMS
DefaultGroupName=AFMS
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=AFMS-Inventory-Setup-{#MyAppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\AFMS Inventory"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\AFMS Inventory"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch AFMS Inventory"; Flags: nowait postinstall skipifsilent

; Records live under %LOCALAPPDATA%\AFMS and are deliberately left in place on
; uninstall, so removing the program never erases the office's records.

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
