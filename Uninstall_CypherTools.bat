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
echo                Cypher Tools - Clean Uninstaller
echo ====================================================================
echo.

:: 1. Remove from Program Files across all Navisworks versions
echo [1/3] Cleaning Program Files Plugins directories...
for /d %%D in ("%ProgramFiles%\Autodesk\Navisworks*") do (
    if exist "%%~fD\Plugins\CypherNavisTools" (
        rmdir /s /q "%%~fD\Plugins\CypherNavisTools" 2>nul
        echo   [-] Removed from %%~nxD\Plugins\CypherNavisTools
    )
    if exist "%%~fD\Plugins\CypherTools" (
        rmdir /s /q "%%~fD\Plugins\CypherTools" 2>nul
        echo   [-] Removed from %%~nxD\Plugins\CypherTools
    )
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
set "BUNDLES=CypherNavisTools.bundle CypherTools.bundle RimoNavisTools.bundle RimoTools.bundle AutomatedClashRunner.bundle"
for %%B in (%BUNDLES%) do (
    if exist "%ProgramData%\Autodesk\ApplicationPlugins\%%B" (
        rmdir /s /q "%ProgramData%\Autodesk\ApplicationPlugins\%%B" 2>nul
        echo   [-] Removed ProgramData\Autodesk\ApplicationPlugins\%%B
    )
)

:: 3. Remove from AppData (User space)
echo.
echo [3/3] Cleaning AppData ApplicationPlugins & User Plugins...
for %%B in (%BUNDLES%) do (
    if exist "%APPDATA%\Autodesk\ApplicationPlugins\%%B" (
        rmdir /s /q "%APPDATA%\Autodesk\ApplicationPlugins\%%B" 2>nul
        echo   [-] Removed AppData\Autodesk\ApplicationPlugins\%%B
    )
)

for /d %%M in ("%APPDATA%\Autodesk\Navisworks Manage*") do (
    for %%P in (CypherNavisTools CypherTools RimoNavisTools RimoTools AutomatedClashRunner) do (
        if exist "%%~fM\Plugins\%%P" (
            rmdir /s /q "%%~fM\Plugins\%%P" 2>nul
            echo   [-] Removed AppData\%%~nxM\Plugins\%%P
        )
    )
)

echo.
echo ====================================================================
echo   SUCCESS! Cypher Tools has been completely removed from Navisworks.
echo   You can now test running Install_CypherTools.bat from a clean slate.
echo ====================================================================
echo.
pause
