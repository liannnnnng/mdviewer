@echo off
REM ============================================================
REM  Build script for "Markdown Viewer" (.NET Framework 4.8 WinForms + WebView2)
REM  Keep this file ASCII-only so it runs on any Windows codepage.
REM
REM  The TARGET MACHINE needs nothing installed: .NET Framework 4.8 is
REM  built into Windows 10 1903+ / Windows 11, and WebView2 ships with it.
REM
REM  Usage:
REM     build.bat              -> build and collect the publish folder
REM  Output: .\publish\MarkdownViewer.exe   (copy the whole publish folder to distribute)
REM
REM  Requires (BUILD ONLY): a .NET SDK (6.0+ recommended, 8.0 works), either
REM  on PATH or pre-installed locally at .tools\dotnet\dotnet.exe
REM  (dotnet-install.ps1 -Channel 8.0 -InstallDir .tools\dotnet -NoPath).
REM ============================================================
setlocal
cd /d "%~dp0"

set "DOTNET=dotnet"
where dotnet >nul 2>nul
if errorlevel 1 (
  if exist "%~dp0.tools\dotnet\dotnet.exe" (
    set "DOTNET=%~dp0.tools\dotnet\dotnet.exe"
  ) else (
    echo [ERROR] dotnet not found on PATH and no local .tools\dotnet. Install a .NET SDK first:
    echo         winget install Microsoft.DotNet.SDK.8
    echo     or: powershell -File .tools\dotnet-install.ps1 -Channel 8.0 -InstallDir .tools\dotnet -NoPath
    pause
    exit /b 1
  )
)

echo Building net48 (framework built into Windows 10/11) ...
"%DOTNET%" publish -c Release -o publish

if errorlevel 1 (
  echo.
  echo [FAILED] dotnet publish returned an error.
  pause
  exit /b 1
)

echo.
echo [OK] Built: %~dp0publish\MarkdownViewer.exe
echo      Copy the whole "publish" folder when distributing.
echo      No runtime install needed on the target machine.
pause
