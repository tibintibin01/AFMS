#define MyAppName "AFMS - Accountable Forms Management System"
#define MyAppVersion "1.0.0"
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
OutputBaseFilename=AFMS-Setup-{#MyAppVersion}-win-x64
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
Name: "{autoprograms}\AFMS"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\AFMS"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch AFMS"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
