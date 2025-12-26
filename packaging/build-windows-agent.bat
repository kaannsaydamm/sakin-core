@echo off
REM
REM SAKIN Windows Agent Build Script
REM Builds the agent and creates an NSIS installer for distribution
REM
REM Usage:
REM   build-windows-agent.bat [--version <version>] [--output <dir>]
REM
REM Prerequisites:
REM   - .NET 8.0 SDK
REM   - NSIS (Nullsoft Scriptable Install System)
REM   - NSIS MUI plugin (for Modern User Interface)
REM

setlocal EnableDelayedExpansion

set VERSION=
set OUTPUT_DIR=%~dp0..\artifacts
set BUILD_CONFIG=Release
set AGENT_NAME=sakin-agent-windows
set AGENT_DIR=sakin-collectors\Sakin.Agents.Windows
set BUILDER_DIR=%~dp0..\sakin-collectors\Sakin.Agents.Windows.Installer

REM Parse command line arguments
:parse_args
if "%~1"=="" goto after_parse
if "%~1"=="--version" (
    set VERSION=%~2
    shift /1
    shift /1
    goto parse_args
)
if "%~1"=="--output" (
    set OUTPUT_DIR=%~2
    shift /1
    shift /1
    goto parse_args
)
if "%~1"=="--help" goto show_help
if "%~1"=="-h" goto show_help
shift /1
goto parse_args

:after_parse

REM Set default version if not provided
if not defined VERSION (
    for /f "delims=" %%i in ('git describe --tags --always 2^>nul') do set VERSION=%%i
    if not defined VERSION set VERSION=1.0.0
)

echo.
echo ========================================
echo SAKIN Windows Agent Build Script
echo ========================================
echo.

REM Check prerequisites
call :check_prerequisites

REM Build the agent
call :build_agent

REM Build the installer
call :build_installer

REM Print summary
call :print_summary

endlocal
exit /b 0

REM ========================================
REM Functions
REM ========================================

:check_prerequisites
echo [INFO] Checking prerequisites...

REM Check .NET SDK
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found. Please install .NET 8.0 SDK.
    exit /b 1
)
for /f "tokens=2 delims= " %%v in ('dotnet --version') do set DOTNET_VERSION=%%v
echo [INFO] .NET SDK version: %DOTNET_VERSION%

REM Check NSIS
where nsis >nul 2>&1
if errorlevel 1 (
    echo [WARN] NSIS not found. Installer will not be built.
    echo [WARN] Install NSIS from https://nsis.sourceforge.io/Download
    set NSIS_FOUND=0
) else (
    for /f "tokens=2" %%v in ('nsis -V2^>^&1') do set NSIS_VERSION=%%v
    echo [INFO] NSIS version: %NSIS_VERSION%
    set NSIS_FOUND=1
)

goto :eof

:build_agent
echo [INFO] Building SAKIN Windows Agent v%VERSION%...

cd %~dp0..\

REM Restore dependencies
echo [INFO] Restoring dependencies...
dotnet restore %AGENT_DIR%\Sakin.Agents.Windows.csproj
if errorlevel 1 (
    echo [ERROR] Failed to restore dependencies
    exit /b 1
)

REM Build in Release mode
echo [INFO] Building agent (%BUILD_CONFIG% mode)...
dotnet publish %AGENT_DIR%\Sakin.Agents.Windows.csproj ^
    -c %BUILD_CONFIG% ^
    -o "%AGENT_DIR%\bin\%BUILD_CONFIG%\net8.0-windows\publish" ^
    -p:Version=%VERSION% ^
    --self-contained false ^
    -r win-x64 ^
    --no-self-contained
if errorlevel 1 (
    echo [ERROR] Build failed
    exit /b 1
)

echo [INFO] Build complete!
goto :eof

:build_installer
if "%NSIS_FOUND%"=="0" (
    echo [WARN] Skipping installer build (NSIS not found)
    goto :eof
)

echo [INFO] Building NSIS installer...

REM Copy configuration files
mkdir "%BUILDER_DIR%\..\..\..\configs\windows" >nul 2>&1
if exist "%~dp0configs\windows\appsettings.json" (
    copy "%~dp0configs\windows\appsettings.json" "%BUILDER_DIR%\..\..\..\configs\windows\" >nul
)

REM Build installer
cd %BUILDER_DIR%
makensis sakin-agent-windows-setup.nsi
if errorlevel 1 (
    echo [ERROR] Installer build failed
    exit /b 1
)

REM Move installer to output directory
mkdir "%OUTPUT_DIR%" >nul 2>&1
move "%BUILDER_DIR%\..\..\..\artifacts\bin\sakin-agent-windows-setup.exe" "%OUTPUT_DIR%\%AGENT_NAME%-v%VERSION%.exe" >nul

echo [INFO] Installer built successfully!
goto :eof

:print_summary
echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo Version: %VERSION%
echo Output: %OUTPUT_DIR%
echo.
echo Files created:
echo   - %AGENT_NAME%-v%VERSION%.exe
echo.
echo To install on Windows:
echo   1. Run the .exe as Administrator
echo   2. Follow the installation wizard
echo   3. Enter your SAKIN endpoint and token
echo.
echo Silent installation:
echo   %AGENT_NAME%-v%VERSION%.exe /S /endpoint=http://server:5001 /token=your-token
echo.
goto :eof

:show_help
echo SAKIN Windows Agent Build Script
echo.
echo Usage: %~nx0 [OPTIONS]
echo.
echo Options:
echo   --version VERSION    Set version number (default: git tag or 1.0.0)
echo   --output DIR         Set output directory (default: ..\artifacts)
echo   --help, -h           Show this help message
echo.
echo Prerequisites:
echo   - .NET 8.0 SDK
echo   - NSIS (Nullsoft Scriptable Install System)
echo.
exit /b 0
