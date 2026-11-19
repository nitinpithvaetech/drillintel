# =====================================================================
# Master Build Script for all DrillIntel Installers
# =====================================================================
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipPublish = $false
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$DrillIntelScript = Join-Path $ScriptDir "DrillIntel\build_installer.ps1"
$LicenseAdminScript = Join-Path $ScriptDir "LicenseAdmin\build_installer.ps1"
$OutputDir = Join-Path $ScriptDir "Output"

Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host " Building All DrillIntel Installers ($Configuration - $Runtime)" -ForegroundColor Magenta
Write-Host "==========================================================" -ForegroundColor Magenta

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# 1. Build DrillIntel Client Installer
& $DrillIntelScript -Configuration $Configuration -Runtime $Runtime -SkipPublish:$SkipPublish

Write-Host ""

# 2. Build License Admin Installer
& $LicenseAdminScript -Configuration $Configuration -Runtime $Runtime -SkipPublish:$SkipPublish

$stopwatch.Stop()

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host " All Installers Built Successfully in $($stopwatch.Elapsed.TotalSeconds.ToString('F1'))s!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Magenta

$installers = Get-ChildItem -Path $OutputDir -Filter "*.exe" | Sort-Object Name
Write-Host ""
$installers | Select-Object Name, @{Name="Size (MB)"; Expression={[Math]::Round($_.Length / 1MB, 2)}}, LastWriteTime | Format-Table -AutoSize
Write-Host "Output Directory: $OutputDir" -ForegroundColor Yellow

