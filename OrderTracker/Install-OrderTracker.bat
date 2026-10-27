@echo off
setlocal
set "INSTALLER=%~dp0Install-OrderTracker.ps1"

if not exist "%INSTALLER%" (
    echo ERROR: Install-OrderTracker.ps1 was not found beside this batch file.
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%INSTALLER%"
set "INSTALL_RESULT=%ERRORLEVEL%"

if not "%INSTALL_RESULT%"=="0" (
    echo.
    echo Installation failed with exit code %INSTALL_RESULT%.
)
pause
exit /b %INSTALL_RESULT%
