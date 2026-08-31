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
echo      Rimo Tools Universal Multi-Version Installer (2020-2026)
echo ====================================================================
echo.

set "ROOT=%~dp0"
set "BIN2023=%ROOT%bin\Release\2023"
set "BIN2024=%ROOT%bin\Release\2024"
set "COUNT=0"

:: 1. Global ProgramData Multi-Version Bundle Deployment
echo [1/3] Deploying Global Multi-Version ApplicationPlugins Bundle...
set "GLOBAL_BUNDLE=%ProgramData%\Autodesk\ApplicationPlugins\RimoNavisTools.bundle"
if exist "%GLOBAL_BUNDLE%" rmdir /s /q "%GLOBAL_BUNDLE%"
mkdir "%GLOBAL_BUNDLE%\Contents\2023\en-US" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2023\Images" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2024\en-US" 2>nul
mkdir "%GLOBAL_BUNDLE%\Contents\2024\Images" 2>nul
mkdir "%GLOBAL_BUNDLE%\en-US" 2>nul
mkdir "%GLOBAL_BUNDLE%\Images" 2>nul

copy /Y "%ROOT%PackageContents.xml" "%GLOBAL_BUNDLE%\" >nul
copy /Y "%ROOT%en-US\RimoRibbon.xaml" "%GLOBAL_BUNDLE%\en-US\" >nul
copy /Y "%ROOT%Images\*.png" "%GLOBAL_BUNDLE%\Images\" >nul

if exist "%BIN2023%\RimoNavisTools.dll" (
    copy /Y "%BIN2023%\RimoNavisTools.dll" "%GLOBAL_BUNDLE%\Contents\2023\" >nul
    copy /Y "%ROOT%en-US\RimoRibbon.xaml" "%GLOBAL_BUNDLE%\Contents\2023\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%GLOBAL_BUNDLE%\Contents\2023\Images\" >nul
)
if exist "%BIN2024%\RimoNavisTools.dll" (
    copy /Y "%BIN2024%\RimoNavisTools.dll" "%GLOBAL_BUNDLE%\Contents\2024\" >nul
    copy /Y "%ROOT%en-US\RimoRibbon.xaml" "%GLOBAL_BUNDLE%\Contents\2024\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "%GLOBAL_BUNDLE%\Contents\2024\Images\" >nul
)
echo      - Global Bundle deployed successfully.

:: 2. Auto-Detect and Deploy to All Program Files Navisworks Installations
echo.
echo [2/3] Scanning Program Files for Autodesk Navisworks Installations...
set "AUTODESK_DIR=%ProgramFiles%\Autodesk"

for /d %%D in ("%AUTODESK_DIR%\Navisworks*") do (
    set "NW_DIR=%%~fD"
    set "NW_NAME=%%~nxD"
    
    :: Clean legacy folders
    if exist "!NW_DIR!\Plugins\AutomatedClashRunner" (
        rmdir /s /q "!NW_DIR!\Plugins\AutomatedClashRunner" 2>nul
    )
    if exist "!NW_DIR!\Plugins\RimoTools" (
        rmdir /s /q "!NW_DIR!\Plugins\RimoTools" 2>nul
    )

    set "TARGET=!NW_DIR!\Plugins\RimoNavisTools"
    if not exist "!TARGET!\en-US" mkdir "!TARGET!\en-US" 2>nul
    if not exist "!TARGET!\Images" mkdir "!TARGET!\Images" 2>nul

    :: Determine Version (2020-2023 vs 2024-2026)
    echo !NW_NAME! | findstr /C:"2024" /C:"2025" /C:"2026" >nul
    if !errorlevel! equ 0 (
        echo   [+] Found !NW_NAME! (Deploying 2024+ engine)
        copy /Y "%BIN2024%\RimoNavisTools.dll" "!TARGET!\" >nul
    ) else (
        echo   [+] Found !NW_NAME! (Deploying 2020-2023 engine)
        copy /Y "%BIN2023%\RimoNavisTools.dll" "!TARGET!\" >nul
    )

    copy /Y "%ROOT%en-US\RimoRibbon.xaml" "!TARGET!\en-US\" >nul
    copy /Y "%ROOT%Images\*.png" "!TARGET!\Images\" >nul
    set /a COUNT+=1
)

:: 3. Finish
echo.
echo [3/3] Finalizing installation...
echo.
echo ====================================================================
echo   SUCCESS! Rimo Tools installed for !COUNT! Navisworks installation(s).
echo   - 2023 Engine: Navisworks 2020, 2021, 2022, 2023
echo   - 2024 Engine: Navisworks 2024, 2025, 2026
echo.
echo   You can now launch Navisworks Manage!
echo ====================================================================
echo.
pause
