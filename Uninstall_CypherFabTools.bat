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
echo    Cypher Fab Tools Clean Uninstaller
echo ====================================================================
echo.

echo Removing Cypher Fab Tools bundle from ProgramData...
if exist "%ProgramData%\Autodesk\ApplicationPlugins\CypherFabTools.bundle" (
    rmdir /s /q "%ProgramData%\Autodesk\ApplicationPlugins\CypherFabTools.bundle"
    echo   - Removed %ProgramData%\Autodesk\ApplicationPlugins\CypherFabTools.bundle
)

echo Removing Cypher Fab Tools bundle from AppData...
if exist "%APPDATA%\Autodesk\ApplicationPlugins\CypherFabTools.bundle" (
    rmdir /s /q "%APPDATA%\Autodesk\ApplicationPlugins\CypherFabTools.bundle"
    echo   - Removed %APPDATA%\Autodesk\ApplicationPlugins\CypherFabTools.bundle
)

echo.
echo ====================================================================
echo UNINSTALL COMPLETE! Cypher Fab Tools has been removed.
echo (Cypher Generic Clash, if installed, was preserved).
echo ====================================================================
pause
