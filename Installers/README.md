# DrillIntel Installers

This directory contains the installer build definitions, Inno Setup 6 scripts, and automated build scripts for the DrillIntel suite.

## Projects

| Installer Project | Target Application | Output Executable | Setup Technology |
| :--- | :--- | :--- | :--- |
| **DrillIntel** | `DrillIntel.csproj` (Main Desktop Client) | `DrillIntel-Setup-v1.0.0-x64.exe` | Inno Setup 6 (LZMA2 Ultra) |
| **LicenseAdmin** | `DrillIntel.Licensing.Admin.csproj` (License Admin Tool) | `DrillIntel-LicenseAdmin-Setup-v1.0.0-x64.exe` | Inno Setup 6 (LZMA2 Ultra) |

---

## Directory Structure

```
Installers/
├── DrillIntel/
│   ├── DrillIntel.iss               # Inno Setup script for the client
│   └── build_installer.ps1          # Builds & packages DrillIntel client
├── LicenseAdmin/
│   ├── LicenseAdmin.iss             # Inno Setup script for License Admin
│   └── build_installer.ps1          # Builds & packages License Admin
├── Output/                          # Compiled setup executables (.exe)
├── Build-All-Installers.ps1         # Master script to publish & compile all installers
└── README.md                        # Documentation
```

---

## How to Build the Installers

### 1. Build Everything (Recommended)
Open PowerShell and run:
```powershell
.\Installers\Build-All-Installers.ps1
```
This will:
1. Publish `DrillIntel` in `Release` `win-x64` self-contained mode.
2. Compile `Installers\DrillIntel\DrillIntel.iss` via `ISCC.exe`.
3. Publish `DrillIntel.Licensing.Admin` in `Release` `win-x64` self-contained mode.
4. Compile `Installers\LicenseAdmin\LicenseAdmin.iss` via `ISCC.exe`.
5. Place both standalone setup `.exe` files into `Installers\Output\`.

### 2. Build Only DrillIntel Client
```powershell
.\Installers\DrillIntel\build_installer.ps1
```

### 3. Build Only License Admin
```powershell
.\Installers\LicenseAdmin\build_installer.ps1
```

### 4. Skip Re-publishing (If binaries are already published)
```powershell
.\Installers\DrillIntel\build_installer.ps1 -SkipPublish
```

---

## Key Features of the Installers
- **Self-Contained Deployment**: Packages the required .NET 8 desktop runtime and native libraries (such as `e_sqlite3.dll`) so client machines don't need manual prerequisite installs.
- **LZMA2 Ultra Compression**: Compresses ~180MB of binaries down to ~45–55MB for rapid downloading and easy distribution.
- **Application Database Protection**: Pre-configures `{commonappdata}\DrillIntel` (`C:\ProgramData\DrillIntel`) with `users-full` access so standard users have full SQLite write permissions. Uses `onlyifdoesntexist` and `uninsneveruninstall` so existing well data and settings are **never overwritten on upgrade and never deleted on uninstall**.
- **Desktop & Start Menu Shortcuts**: Generates Start Menu entries and optional desktop shortcuts with the official application icon.
- **Windows Add/Remove Programs Integration**: Registers clean uninstallers in Windows Settings / Control Panel.

