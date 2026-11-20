; =====================================================================
; DrillIntel License Admin Inno Setup Script
; Product: DrillIntel License Administrator
; Company: e-Tech International
; =====================================================================

#define MyAppName "DrillIntel License Admin"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "e-Tech International"
#define MyAppURL "https://etechinter.com"
#define MyAppSupportURL "https://etechinter.com/products/vumaxpro-real-time-drilling-intelligence/"
#define MyAppExeName "DrillIntel.Licensing.Admin.exe"
#define MyAppId "{{A7D482F1-4E12-4C92-881E-2A4B1D6C8E21}"

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
OutputBaseFilename=DrillIntel-LicenseAdmin-Setup-v{#MyAppVersion}-x64

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

[Files]
; Main published binaries and dependencies
Source: "..\..\DrillIntel.Licensing\DrillIntel.Licensing.Admin\bin\Publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

