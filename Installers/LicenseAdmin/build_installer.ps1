# =====================================================================
# Build and Package Script for DrillIntel License Admin
# =====================================================================
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipPublish = $false
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectDir = Resolve-Path (Join-Path $ScriptDir "..\..\DrillIntel.Licensing\DrillIntel.Licensing.Admin")
$SolutionDir = Resolve-Path (Join-Path $ScriptDir "..\..")
$PublishDir = Join-Path $ProjectDir "bin\Publish\$Runtime"
$IssFile = Join-Path $ScriptDir "LicenseAdmin.iss"
$OutputDir = Join-Path $ScriptDir "..\Output"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Building License Admin Installer ($Configuration - $Runtime)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Locate Inno Setup Compiler
$InnoCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
)

$Iscc = $InnoCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $Iscc) {
    throw "Inno Setup Compiler (ISCC.exe) was not found. Please verify Inno Setup 6 is installed."
}

Write-Host "[1/3] Located Inno Setup Compiler: $Iscc" -ForegroundColor Green

# 2. Publish Project (Self-contained)
if (-not $SkipPublish) {
    Write-Host "[2/3] Publishing DrillIntel License Admin (.NET 8 self-contained)..." -ForegroundColor Yellow
    $csproj = Join-Path $ProjectDir "DrillIntel.Licensing.Admin.csproj"
    
    $publishArgs = @(
        "publish",
        "`"$csproj`"",
        "-c", $Configuration,
        "-r", $Runtime,
        "--self-contained", "true",
        "-o", "`"$PublishDir`""
    )
    
    $proc = Start-Process -FilePath "dotnet" -ArgumentList $publishArgs -NoNewWindow -PassThru -Wait
    if ($proc.ExitCode -ne 0) {
        throw "dotnet publish failed with exit code $($proc.ExitCode)."
    }
    Write-Host "       Publish completed successfully to: $PublishDir" -ForegroundColor Green
} else {
    Write-Host "[2/3] Skipping publish step as requested." -ForegroundColor Gray
}

# 3. Compile Inno Setup Script
Write-Host "[3/3] Compiling Inno Setup script..." -ForegroundColor Yellow
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$compileArgs = @("/Qp", "`"$IssFile`"")
$isccProc = Start-Process -FilePath $Iscc -ArgumentList $compileArgs -NoNewWindow -PassThru -Wait

if ($isccProc.ExitCode -ne 0) {
    throw "Inno Setup compilation failed with exit code $($isccProc.ExitCode)."
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " DrillIntel License Admin Installer successfully built!" -ForegroundColor Green
$installer = Get-ChildItem -Path $OutputDir -Filter "DrillIntel-LicenseAdmin-Setup-*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($installer) {
    $sizeMb = [Math]::Round($installer.Length / 1MB, 2)
    Write-Host " Installer File: $($installer.FullName)" -ForegroundColor White
    Write-Host " File Size:      $sizeMb MB" -ForegroundColor White
}
Write-Host "==========================================================" -ForegroundColor Cyan

