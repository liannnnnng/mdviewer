@echo off
REM ============================================================
REM  Runs the JsonLite regression suite with the C# compiler that
REM  ships with Windows itself (C:\Windows\Microsoft.NET\Framework64),
REM  so no SDK or toolchain install is needed to run the tests.
REM
REM  JsonLite.cs and JsonTest.cs are intentionally written in the
REM  C# 5 subset supported by that compiler; the main project compiles
REM  them with the modern Roslyn compiler via `dotnet build`.
REM ============================================================
setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [ERROR] .NET Framework 4.x csc.exe not found.
  exit /b 1
)

if not exist out mkdir out
"%CSC%" -nologo -out:out\JsonTest.exe -r:System.dll ..\JsonLite.cs JsonTest.cs
if errorlevel 1 exit /b 1

out\JsonTest.exe
