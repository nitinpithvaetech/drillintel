@echo off
setlocal

echo Building the DrillIntel customer installer (Release, win-x64)...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_installer.ps1" -Configuration Release -Runtime win-x64
if errorlevel 1 goto failed

echo.
echo DrillIntel customer installer build completed successfully.
pause
exit /b 0

:failed
echo.
echo DrillIntel customer installer build failed. Review the errors above.
pause
exit /b 1
