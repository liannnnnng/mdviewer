@echo off
REM ============================================================
REM  Build script for "Markdown Viewer" (.NET WinForms + WebView2)
REM  Keep this file ASCII-only so it runs on any Windows codepage.
REM
REM  Usage:
REM     build.bat              -> framework-dependent build (small, needs .NET 8 Desktop Runtime)
REM     build.bat sc           -> self-contained build (no runtime needed, larger)
REM  Output: .\publish\MarkdownViewer.exe   (copy the whole publish folder to distribute)
REM
REM  Requires: .NET SDK 8 (dotnet on PATH). Check:  dotnet --version
REM ============================================================
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] dotnet not found on PATH. Install .NET 8 SDK first.
  echo         winget install Microsoft.DotNet.SDK.8
  pause
  exit /b 1
)

if /I "%~1"=="sc" (
  echo Building SELF-CONTAINED win-x64 ...
  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
) else (
  echo Building FRAMEWORK-DEPENDENT ...
  dotnet publish -c Release -o publish
)

if errorlevel 1 (
  echo.
  echo [FAILED] dotnet publish returned an error.
  pause
  exit /b 1
)

echo.
echo [OK] Built: %~dp0publish\MarkdownViewer.exe
echo      Copy the whole "publish" folder when distributing.
pause
