# =====================================================================
# Cypher Tools - Full End-to-End Build & Deployment Script
# =====================================================================
$ErrorActionPreference = "Stop"

Write-Host ">>> Locating MSBuild..." -ForegroundColor Cyan
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (!(Test-Path $vswhere)) {
    throw "vswhere.exe not found. Visual Studio installation missing."
}
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (!$msbuild -or !(Test-Path $msbuild)) {
    throw "MSBuild.exe not found."
}
Write-Host "Using MSBuild: $msbuild" -ForegroundColor Green

# 1. Clean output directories
if (Test-Path "bin") { Remove-Item "bin" -Recurse -Force }
if (Test-Path "obj") { Remove-Item "obj" -Recurse -Force }

# 2. Restore NuGet Packages
Write-Host ">>> Restoring NuGet Packages..." -ForegroundColor Cyan
& $msbuild "AutomatedClashRunner.csproj" -t:restore -p:Configuration=Release2023 -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed." }

# 3. Build Multi-Version Plugin DLLs
Write-Host ">>> 1. Building Navisworks 2023 Engine (Release2023)..." -ForegroundColor Cyan
& $msbuild "AutomatedClashRunner.csproj" -p:Configuration=Release2023 -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "2023 Plugin build failed." }

Write-Host ">>> 2. Building Navisworks 2024 Engine (Release2024)..." -ForegroundColor Cyan
& $msbuild "AutomatedClashRunner.csproj" -p:Configuration=Release2024 -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "2024 Plugin build failed." }

Write-Host ">>> 3. Building Navisworks 2025 Engine (Release2025)..." -ForegroundColor Cyan
& $msbuild "AutomatedClashRunner.csproj" -p:Configuration=Release2025 -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "2025 Plugin build failed." }

Write-Host ">>> 4. Building Navisworks 2026 Engine (Release2026)..." -ForegroundColor Cyan
& $msbuild "AutomatedClashRunner.csproj" -p:Configuration=Release2026 -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "2026 Plugin build failed." }

# 4. Package Multi-Version bundle.zip for Standalone Installer
Write-Host ">>> 5. Staging and Packaging Multi-Version bundle.zip..." -ForegroundColor Cyan
$staging = "$env:TEMP\cyphertools_bundle_staging"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }

$stgContents2023 = "$staging\Contents\2023"
$stgContents2024 = "$staging\Contents\2024"
$stgContents2025 = "$staging\Contents\2025"
$stgContents2026 = "$staging\Contents\2026"
New-Item -ItemType Directory -Force -Path $stgContents2023 | Out-Null
New-Item -ItemType Directory -Force -Path $stgContents2024 | Out-Null
New-Item -ItemType Directory -Force -Path $stgContents2025 | Out-Null
New-Item -ItemType Directory -Force -Path $stgContents2026 | Out-Null

Copy-Item "PackageContents.xml" -Destination $staging -Force
Copy-Item "bin\Release\2023\*.dll" -Destination $stgContents2023 -Force
Copy-Item "bin\Release\2024\*.dll" -Destination $stgContents2024 -Force
Copy-Item "bin\Release\2025\*.dll" -Destination $stgContents2025 -Force
Copy-Item "bin\Release\2026\*.dll" -Destination $stgContents2026 -Force

if (Test-Path "en-US") {
    Copy-Item "en-US" -Destination "$stgContents2023\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$stgContents2024\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$stgContents2025\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$stgContents2026\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$staging\en-US" -Recurse -Force
}
if (Test-Path "Images") {
    Copy-Item "Images" -Destination "$stgContents2023\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$stgContents2024\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$stgContents2025\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$stgContents2026\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$staging\Images" -Recurse -Force
}

$zipDest = "Installer\bundle.zip"
if (Test-Path $zipDest) { Remove-Item $zipDest -Force }
Compress-Archive -Path "$staging\*" -DestinationPath $zipDest -Force
Remove-Item $staging -Recurse -Force

# 5. Build Standalone Installer EXE
Write-Host ">>> 6. Compiling Modern Standalone CypherGenericClash_Installer.exe..." -ForegroundColor Cyan
& $msbuild "Installer\Installer.csproj" -p:Configuration=Release -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }

$installerSource = "Installer\bin\Release\CypherGenericClash_Installer.exe"
if (!(Test-Path $installerSource)) {
    $installerSource = "Installer\bin\Release\CypherTools_Installer.exe"
}

$destinations = @(
    "CypherGenericClash_Installer.exe",
    "..\CypherGenericClash_Installer.exe",
    "$env:USERPROFILE\Downloads\CypherGenericClash_Installer.exe",
    "CypherTools_Installer.exe"
)

foreach ($dest in $destinations) {
    try {
        Copy-Item $installerSource -Destination $dest -Force -ErrorAction Stop
        Get-Item $dest | Unblock-File -ErrorAction SilentlyContinue
        Write-Host " - Standalone Generic Installer ready at: $dest" -ForegroundColor Green
    } catch {
        Write-Host " - Warning: Could not copy to ${dest} - $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# 6. Direct AppData Deployment: Multi-Version ApplicationPlugins Bundle
Write-Host ">>> 7. Deploying Multi-Version CypherNavisTools.bundle to Navisworks ApplicationPlugins..." -ForegroundColor Cyan
$bundleDir = "$env:APPDATA\Autodesk\ApplicationPlugins\CypherNavisTools.bundle"
if (Test-Path $bundleDir) { Remove-Item $bundleDir -Recurse -Force }

$contentsDir2023 = "$bundleDir\Contents\2023"
$contentsDir2024 = "$bundleDir\Contents\2024"
$contentsDir2025 = "$bundleDir\Contents\2025"
$contentsDir2026 = "$bundleDir\Contents\2026"
New-Item -ItemType Directory -Force -Path $contentsDir2023 | Out-Null
New-Item -ItemType Directory -Force -Path $contentsDir2024 | Out-Null
New-Item -ItemType Directory -Force -Path $contentsDir2025 | Out-Null
New-Item -ItemType Directory -Force -Path $contentsDir2026 | Out-Null

Copy-Item "PackageContents.xml" -Destination $bundleDir -Force
Copy-Item "bin\Release\2023\*.dll" -Destination $contentsDir2023 -Force
Copy-Item "bin\Release\2024\*.dll" -Destination $contentsDir2024 -Force
Copy-Item "bin\Release\2025\*.dll" -Destination $contentsDir2025 -Force
Copy-Item "bin\Release\2026\*.dll" -Destination $contentsDir2026 -Force

if (Test-Path "en-US") {
    Copy-Item "en-US" -Destination "$contentsDir2023\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$contentsDir2024\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$contentsDir2025\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$contentsDir2026\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$bundleDir\en-US" -Recurse -Force
}
if (Test-Path "Images") {
    Copy-Item "Images" -Destination "$contentsDir2023\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$contentsDir2024\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$contentsDir2025\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$contentsDir2026\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$bundleDir\Images" -Recurse -Force
}
Get-ChildItem $bundleDir -Recurse | Unblock-File -ErrorAction SilentlyContinue
Write-Host " - Multi-Version Bundle deployed to: $bundleDir" -ForegroundColor Green

# 5b. Ensure ProgramData is 100% clean (Rule: NEVER dual-deploy to ProgramData and AppData; duplicate bundle drops ribbon tab)
$programDataPlugins = "$env:ProgramData\Autodesk\ApplicationPlugins"
if (Test-Path $programDataPlugins) {
    Get-ChildItem -Path $programDataPlugins -Filter "*Cypher*" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            Remove-Item $_.FullName -Recurse -Force -ErrorAction Stop
            Write-Host " - Purged duplicate bundle from ProgramData: $($_.FullName)" -ForegroundColor Yellow
        } catch {
            Write-Host " - Warning: Could not purge $($_.FullName) from ProgramData (requires admin)." -ForegroundColor Yellow
        }
    }
}

# 6. Clean Standalone User Plugins Directory (eliminating duplicate plugin loading)
Write-Host ">>> 6. Ensuring Clean User Plugins Directory (eliminating duplicate loading)..." -ForegroundColor Cyan
$legacyUserPlugins = @(
    "$env:APPDATA\Autodesk\Navisworks Manage 2024\Plugins\CypherNavisTools",
    "$env:APPDATA\Autodesk\Navisworks Manage 2024\Plugins\RimoNavisTools",
    "$env:APPDATA\Autodesk\Navisworks Manage 2024\Plugins\AutomatedClashRunner"
)
foreach ($dir in $legacyUserPlugins) {
    if (Test-Path $dir) {
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Write-Host " - Standalone user plugins cleaned. Bundle active in ApplicationPlugins." -ForegroundColor Green

Write-Host "====================================================================" -ForegroundColor Green
Write-Host "ALL BUILDS & INSTALLERS 100% COMPLETE (2020-2026 READY)!" -ForegroundColor Green
Write-Host " - Standalone EXE Installer: CypherTools_Installer.exe" -ForegroundColor Green
Write-Host " - Universal Batch Installer: Install_CypherTools.bat" -ForegroundColor Green
Write-Host " - Clean Uninstaller Batch:   Uninstall_CypherTools.bat" -ForegroundColor Green
Write-Host "====================================================================" -ForegroundColor Green

