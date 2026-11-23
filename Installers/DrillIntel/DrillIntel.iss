; =====================================================================
; DrillIntel Inno Setup Script
; Product: DrillIntel - Desktop Client
; Company: e-Tech International
; =====================================================================

#define MyAppName "DrillIntel"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "e-Tech International"
#define MyAppURL "https://etechinter.com"
#define MyAppSupportURL "https://etechinter.com/products/vumaxpro-real-time-drilling-intelligence/"
#define MyAppExeName "DrillIntel.exe"
#define MyAppId "{{B6E596D3-98C3-4B81-996A-1B3A0E5C9F10}"

[Setup]
; Basic Application Info
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppSupportURL}
AppUpdatesURL={#MyAppURL}

; Destination Directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Modern Styling & Visuals
WizardStyle=modern
SetupIconFile=..\..\DrillIntel\Assets\app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Output Configuration
OutputDir=..\Output
OutputBaseFilename=DrillIntel-Setup-v{#MyAppVersion}-x64

; Compression Settings (LZMA2 Ultra)
Compression=lzma2/ultra64
SolidCompression=yes

; Target Architecture (64-bit .NET 8)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Permissions & Process Control
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
; Ensure common application data directory exists and has full access for all users
Name: "{commonappdata}\{#MyAppName}"; Permissions: users-full

[Files]
; Main published binaries and dependencies
Source: "..\..\DrillIntel\bin\Publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Seed ProgramData master database on initial installation (never overwrite user data on upgrade, never delete on uninstall)
Source: "..\..\DrillIntel\bin\Publish\win-x64\Data\DrillIntelApp.sqlite"; DestDir: "{commonappdata}\{#MyAppName}"; Flags: onlyifdoesntexist uninsneveruninstall; Permissions: users-full

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

