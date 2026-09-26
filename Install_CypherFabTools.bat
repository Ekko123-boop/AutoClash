@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

:: Check for Administrator privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

echo ====================================================================
echo    Cypher Fab Tools Universal Multi-Version Installer (2020-2026)
echo ====================================================================
echo.

set "ROOT=%~dp0"
set "BIN2023=%ROOT%bin\Release\2023"
set "BIN2024=%ROOT%bin\Release\2024"
set "BIN2025=%ROOT%bin\Release\2025"
set "BIN2026=%ROOT%bin\Release\2026"

:: 0. Clean legacy User AppData plugins to prevent duplicate loading
for /d %%M in ("%APPDATA%\Autodesk\Navisworks Manage*") do (
    for %%P in (CypherNavisTools CypherTools RimoNavisTools RimoTools AutomatedClashRunner) do (
        if exist "%%~fM\Plugins\%%P" rmdir /s /q "%%~fM\Plugins\%%P" 2>nul
    )
)

:: 1. Global ProgramData Multi-Version Bundle Deployment
echo [1/3] Deploying Global Multi-Version ApplicationPlugins Bundle...
set "GLOBAL_BUNDLE=%ProgramData%\Autodesk\ApplicationPlugins\CypherFabTools.bundle"
if exist "%GLOBAL_BUNDLE%" rmdir /s /q "%GLOBAL_BUNDLE%"
mkdir "%GLOBAL_BUNDLE%\Contents\2023\en-US" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2023\Images" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2024\en-US" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2024\Images" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2025\en-US" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2025\Images" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2026\en-US" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2026\Images" 2>nul
mkdir "%GLOBAL_BUNDLE%\en-US" 2>nul
mkdir "%GLOBAL_BUNDLE%\Images" 2>nul

copy /Y "%ROOT%PackageContents.xml" "%GLOBAL_BUNDLE%\" >nul
copy /Y "%ROOT%en-US\*.xaml" "%GLOBAL_BUNDLE%\en-US\" >nul
copy /Y "%ROOT%Images\*.png" "%GLOBAL_BUNDLE%\Images\" >nul

if exist "%BIN2023%\CypherFabTools.dll" (
    copy /Y "%BIN2023%\CypherFabTools.dll" "%GLOBAL_BUNDLE%\Contents\2023\" >nul
    copy /Y "%ROOT%en-US\*.xaml" "%GLOBAL_BUNDLE%\Contents\2023\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%GLOBAL_BUNDLE%\Contents\2023\Images\" >nul
)
if exist "%BIN2024%\CypherFabTools.dll" (
    copy /Y "%BIN2024%\CypherFabTools.dll" "%GLOBAL_BUNDLE%\Contents\2024\" >nul
    copy /Y "%ROOT%en-US\*.xaml" "%GLOBAL_BUNDLE%\Contents\2024\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%GLOBAL_BUNDLE%\Contents\2024\Images\" >nul
)
if exist "%BIN2025%\CypherFabTools.dll" (
    copy /Y "%BIN2025%\CypherFabTools.dll" "%GLOBAL_BUNDLE%\Contents\2025\" >nul
    copy /Y "%ROOT%en-US\*.xaml" "%GLOBAL_BUNDLE%\Contents\2025\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%GLOBAL_BUNDLE%\Contents\2025\Images\" >nul
)
if exist "%BIN2026%\CypherFabTools.dll" (
    copy /Y "%BIN2026%\CypherFabTools.dll" "%GLOBAL_BUNDLE%\Contents\2026\" >nul
    copy /Y "%ROOT%en-US\*.xaml" "%GLOBAL_BUNDLE%\Contents\2026\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%GLOBAL_BUNDLE%\Contents\2026\Images\" >nul
)
echo      - Global Bundle deployed successfully.

:: 2. Purge legacy duplicates
echo.
echo [2/3] Purging any legacy standalone plugins...
for %%B in (CypherNavisTools.bundle CypherTools.bundle RimoNavisTools.bundle RimoTools.bundle AutomatedClashRunner.bundle) do (
    if exist "%ProgramData%\Autodesk\ApplicationPlugins\%%B" rmdir /s /q "%ProgramData%\Autodesk\ApplicationPlugins\%%B" 2>nul
    if exist "%APPDATA%\Autodesk\ApplicationPlugins\%%B" rmdir /s /q "%APPDATA%\Autodesk\ApplicationPlugins\%%B" 2>nul
)

:: 3. Unblock files
echo.
echo [3/3] Unblocking DLLs and files...
powershell -Command "Get-ChildItem -Path '%GLOBAL_BUNDLE%' -Recurse | Unblock-File -ErrorAction SilentlyContinue"

echo.
echo ====================================================================
echo INSTALLATION COMPLETE! Cypher Fab Tools is ready for Navisworks.
echo ====================================================================
pause
