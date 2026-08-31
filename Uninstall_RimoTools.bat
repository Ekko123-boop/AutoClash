@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

:: Check for Administrator privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator privileges to clean Program Files...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

echo ====================================================================
echo                Rimo Tools - Clean Uninstaller
echo ====================================================================
echo.

:: 1. Remove from Program Files across all Navisworks versions
echo [1/3] Cleaning Program Files Plugins directories...
for /d %%D in ("%ProgramFiles%\Autodesk\Navisworks*") do (
    if exist "%%~fD\Plugins\RimoNavisTools" (
        rmdir /s /q "%%~fD\Plugins\RimoNavisTools" 2>nul
        echo   [-] Removed from %%~nxD\Plugins\RimoNavisTools
    )
    if exist "%%~fD\Plugins\RimoTools" (
        rmdir /s /q "%%~fD\Plugins\RimoTools" 2>nul
        echo   [-] Removed from %%~nxD\Plugins\RimoTools
    )
    if exist "%%~fD\Plugins\AutomatedClashRunner" (
        rmdir /s /q "%%~fD\Plugins\AutomatedClashRunner" 2>nul
        echo   [-] Removed from %%~nxD\Plugins\AutomatedClashRunner
    )
)

:: 2. Remove from ProgramData ApplicationPlugins
echo.
echo [2/3] Cleaning ProgramData ApplicationPlugins bundles...
if exist "%ProgramData%\Autodesk\ApplicationPlugins\RimoNavisTools.bundle" (
    rmdir /s /q "%ProgramData%\Autodesk\ApplicationPlugins\RimoNavisTools.bundle" 2>nul
    echo   [-] Removed ProgramData\Autodesk\ApplicationPlugins\RimoNavisTools.bundle
)
if exist "%ProgramData%\Autodesk\ApplicationPlugins\RimoTools.bundle" (
    rmdir /s /q "%ProgramData%\Autodesk\ApplicationPlugins\RimoTools.bundle" 2>nul
    echo   [-] Removed ProgramData\Autodesk\ApplicationPlugins\RimoTools.bundle
)
if exist "%ProgramData%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle" (
    rmdir /s /q "%ProgramData%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle" 2>nul
    echo   [-] Removed ProgramData\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle
)

:: 3. Remove from AppData (User space)
echo.
echo [3/3] Cleaning AppData ApplicationPlugins & User Plugins...
if exist "%APPDATA%\Autodesk\ApplicationPlugins\RimoNavisTools.bundle" (
    rmdir /s /q "%APPDATA%\Autodesk\ApplicationPlugins\RimoNavisTools.bundle" 2>nul
    echo   [-] Removed AppData\Autodesk\ApplicationPlugins\RimoNavisTools.bundle
)
if exist "%APPDATA%\Autodesk\ApplicationPlugins\RimoTools.bundle" (
    rmdir /s /q "%APPDATA%\Autodesk\ApplicationPlugins\RimoTools.bundle" 2>nul
    echo   [-] Removed AppData\Autodesk\ApplicationPlugins\RimoTools.bundle
)
if exist "%APPDATA%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle" (
    rmdir /s /q "%APPDATA%\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle" 2>nul
    echo   [-] Removed AppData\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle
)
if exist "%APPDATA%\Autodesk\Navisworks Manage 2024\Plugins\RimoNavisTools" (
    rmdir /s /q "%APPDATA%\Autodesk\Navisworks Manage 2024\Plugins\RimoNavisTools" 2>nul
    echo   [-] Removed AppData\Autodesk\Navisworks Manage 2024\Plugins\RimoNavisTools
)
if exist "%APPDATA%\Autodesk\Navisworks Manage 2024\Plugins\RimoTools" (
    rmdir /s /q "%APPDATA%\Autodesk\Navisworks Manage 2024\Plugins\RimoTools" 2>nul
    echo   [-] Removed AppData\Autodesk\Navisworks Manage 2024\Plugins\RimoTools
)

echo.
echo ====================================================================
echo   SUCCESS! Rimo Tools has been completely removed from Navisworks.
echo   You can now test running Install_RimoTools.bat from a clean slate.
echo ====================================================================
echo.
pause
