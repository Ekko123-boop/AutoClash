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
set "COUNT=0"

:: 0. Clean legacy User AppData plugins to prevent duplicate loading
for /d %%M in ("%APPDATA%\Autodesk\Navisworks Manage*") do (
    for %%P in (CypherNavisTools CypherTools RimoNavisTools RimoTools AutomatedClashRunner) do (
        if exist "%%~fM\Plugins\%%P" rmdir /s /q "%%~fM\Plugins\%%P" 2>nul
    )
)

:: 1. Global ProgramData Multi-Version Bundle Deployment
echo [1/3] Deploying Global Multi-Version ApplicationPlugins Bundle...
set "GLOBAL_BUNDLE=%ProgramData%\Autodesk\ApplicationPlugins\CypherNavisTools.bundle"
if exist "%GLOBAL_BUNDLE%" rmdir /s /q "%GLOBAL_BUNDLE%"
mkdir "%GLOBAL_BUNDLE%\Contents\2023\en-US" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2023\Images" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2024\en-US" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2024\Images" 2>nul
mkdir "%GLOBAL_BUNDLE%\en-US" 2>nul
mkdir "%GLOBAL_BUNDLE%\Images" 2>nul

copy /Y "%ROOT%PackageContents.xml" "%GLOBAL_BUNDLE%\" >nul
copy /Y "%ROOT%en-US\*.xaml" "%GLOBAL_BUNDLE%\en-US\" >nul
copy /Y "%ROOT%Images\*.png" "%GLOBAL_BUNDLE%\Images\" >nul

if exist "%BIN2023%\CypherNavisTools.dll" (
    copy /Y "%BIN2023%\CypherNavisTools.dll" "%GLOBAL_BUNDLE%\Contents\2023\" >nul
    copy /Y "%ROOT%en-US\*.xaml" "%GLOBAL_BUNDLE%\Contents\2023\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%GLOBAL_BUNDLE%\Contents\2023\Images\" >nul
)
if exist "%BIN2024%\CypherNavisTools.dll" (
    copy /Y "%BIN2024%\CypherNavisTools.dll" "%GLOBAL_BUNDLE%\Contents\2024\" >nul
    copy /Y "%ROOT%en-US\*.xaml" "%GLOBAL_BUNDLE%\Contents\2024\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%GLOBAL_BUNDLE%\Contents\2024\Images\" >nul
)
echo      - Global Bundle deployed successfully.

:: 2. Purge duplicate standalone plugins from Program Files and User AppData
echo.
echo [2/3] Purging any duplicate standalone plugins to prevent dual-load conflicts...
set "AUTODESK_DIR=%ProgramFiles%\Autodesk"

for /d %%D in ("%AUTODESK_DIR%\Navisworks*") do (
    for %%P in (CypherNavisTools CypherTools RimoNavisTools RimoTools AutomatedClashRunner) do (
        if exist "%%~fD\Plugins\%%P" (
            rmdir /s /q "%%~fD\Plugins\%%P" 2>nul
            echo      - Cleaned duplicate plugin: %%~nxD\Plugins\%%P
        )
    )
)

:: Also purge user AppData bundle so there is only one authoritative bundle on the system
if exist "%APPDATA%\Autodesk\ApplicationPlugins\CypherNavisTools.bundle" (
    rmdir /s /q "%APPDATA%\Autodesk\ApplicationPlugins\CypherNavisTools.bundle" 2>nul
    echo      - Cleaned duplicate user bundle from AppData.
)

:: 3. Finish
echo.
echo [3/3] Finalizing installation...
echo.
echo ====================================================================
echo   SUCCESS! Cypher Tools installed for !COUNT! Navisworks installation(s).
echo   - 2023 Engine: Navisworks 2020, 2021, 2022, 2023
echo   - 2024 Engine: Navisworks 2024, 2025, 2026
echo.
echo   You can now launch Navisworks Manage!
echo ====================================================================
echo.
pause
