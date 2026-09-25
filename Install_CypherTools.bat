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
echo      Cypher Tools Universal Multi-Version Installer (2020-2026)
echo ====================================================================
echo.

set "ROOT=%~dp0"
set "BIN2023=%ROOT%bin\Release\2023"
set "BIN2024=%ROOT%bin\Release\2024"
set "BIN2025=%ROOT%bin\Release\2025"
set "BIN2026=%ROOT%bin\Release\2026"
set "COUNT=0"

:: 0. Clean legacy User AppData plugins to prevent duplicate loading
for /d %%M in ("%APPDATA%\Autodesk\Navisworks Manage*") do (
    for %%P in (CypherNavisTools CypherTools RimoNavisTools RimoTools AutomatedClashRunner) do (
        if exist "%%~fM\Plugins\%%P" rmdir /s /q "%%~fM\Plugins\%%P" 2>nul
    )
)

:: 1. Purge duplicate plugins & bundles from ProgramData and Program Files
echo [1/2] Purging conflicting bundles and standalone plugins...
for %%B in (CypherNavisTools CypherNavisTools_backup CypherTools RimoNavisTools RimoTools AutomatedClashRunner) do (
    if exist "%ProgramData%\Autodesk\ApplicationPlugins\%%B" (
        attrib -r -s -h "%ProgramData%\Autodesk\ApplicationPlugins\%%B\*.*" /s /d >nul 2>&1
        rmdir /s /q "%ProgramData%\Autodesk\ApplicationPlugins\%%B" 2>nul
        echo      - Removed ProgramData bundle: %%B
    )
    if exist "%ProgramData%\Autodesk\ApplicationPlugins\%%B.bundle" (
        attrib -r -s -h "%ProgramData%\Autodesk\ApplicationPlugins\%%B.bundle\*.*" /s /d >nul 2>&1
        rmdir /s /q "%ProgramData%\Autodesk\ApplicationPlugins\%%B.bundle" 2>nul
        echo      - Removed ProgramData bundle: %%B.bundle
    )
)

set "AUTODESK_DIR=%ProgramFiles%\Autodesk"
for /d %%D in ("%AUTODESK_DIR%\Navisworks*") do (
    for %%P in (CypherNavisTools CypherTools RimoNavisTools RimoTools AutomatedClashRunner) do (
        if exist "%%~fD\Plugins\%%P" (
            rmdir /s /q "%%~fD\Plugins\%%P" 2>nul
            echo      - Cleaned duplicate plugin: %%~nxD\Plugins\%%P
        )
    )
)

:: 2. Authoritative AppData Bundle Deployment (Rule: Single target avoids ribbon tab collision)
echo.
echo [2/2] Deploying Multi-Version ApplicationPlugins Bundle to AppData...
set "USER_BUNDLE=%APPDATA%\Autodesk\ApplicationPlugins\CypherNavisTools.bundle"
if exist "%USER_BUNDLE%" rmdir /s /q "%USER_BUNDLE%"
mkdir "%USER_BUNDLE%\Contents\2023\en-US" 2>nul
mkdir "%USER_BUNDLE%\Contents\2023\Images" 2>nul
mkdir "%USER_BUNDLE%\Contents\2024\en-US" 2>nul
mkdir "%USER_BUNDLE%\Contents\2024\Images" 2>nul
mkdir "%USER_BUNDLE%\Contents\2025\en-US" 2>nul
mkdir "%USER_BUNDLE%\Contents\2025\Images" 2>nul
mkdir "%USER_BUNDLE%\Contents\2026\en-US" 2>nul
mkdir "%USER_BUNDLE%\Contents\2026\Images" 2>nul
mkdir "%USER_BUNDLE%\en-US" 2>nul
mkdir "%USER_BUNDLE%\Images" 2>nul

copy /Y "%ROOT%PackageContents.xml" "%USER_BUNDLE%\" >nul
copy /Y "%ROOT%en-US\*.xaml" "%USER_BUNDLE%\en-US\" >nul
copy /Y "%ROOT%Images\*.png" "%USER_BUNDLE%\Images\" >nul

if exist "%BIN2023%\CypherNavisTools.dll" (
    copy /Y "%BIN2023%\CypherNavisTools.dll" "%USER_BUNDLE%\Contents\2023\" >nul
    copy /Y "%ROOT%en-US\*.xaml" "%USER_BUNDLE%\Contents\2023\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%USER_BUNDLE%\Contents\2023\Images\" >nul
)
if exist "%BIN2024%\CypherNavisTools.dll" (
    copy /Y "%BIN2024%\CypherNavisTools.dll" "%USER_BUNDLE%\Contents\2024\" >nul
    copy /Y "%ROOT%en-US\*.xaml" "%USER_BUNDLE%\Contents\2024\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%USER_BUNDLE%\Contents\2024\Images\" >nul
)
if exist "%BIN2025%\CypherNavisTools.dll" (
    copy /Y "%BIN2025%\CypherNavisTools.dll" "%USER_BUNDLE%\Contents\2025\" >nul
    copy /Y "%ROOT%en-US\*.xaml" "%USER_BUNDLE%\Contents\2025\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%USER_BUNDLE%\Contents\2025\Images\" >nul
)
if exist "%BIN2026%\CypherNavisTools.dll" (
    copy /Y "%BIN2026%\CypherNavisTools.dll" "%USER_BUNDLE%\Contents\2026\" >nul
    copy /Y "%ROOT%en-US\*.xaml" "%USER_BUNDLE%\Contents\2026\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%USER_BUNDLE%\Contents\2026\Images\" >nul
)
echo      - User AppData Bundle deployed successfully.

echo.
echo ====================================================================
echo   SUCCESS! Cypher Tools deployed cleanly to ApplicationPlugins.
echo   - 2023 Engine: Navisworks 2020, 2021, 2022, 2023
echo   - 2024 Engine: Navisworks 2024
echo   - 2025 Engine: Navisworks 2025
echo   - 2026 Engine: Navisworks 2026
echo   - Location: %USER_BUNDLE%
echo.
echo   You can now launch Navisworks Manage!
echo ====================================================================
echo.
pause
