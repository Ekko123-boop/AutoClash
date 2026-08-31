# =====================================================================
# AutomatedClashRunner - Full End-to-End Build & Deployment Script
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

# 1. Build Plugin DLL
Write-Host ">>> 1. Building AutomatedClashRunner.dll (Release x64)..." -ForegroundColor Cyan
& $msbuild "AutomatedClashRunner.csproj" -p:Configuration=Release -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Plugin build failed." }

# 2. Package bundle.zip for Installer
Write-Host ">>> 2. Packaging bundle.zip..." -ForegroundColor Cyan
$staging = "$env:TEMP\acr_bundle_staging"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path "$staging\Contents\AutomatedClashRunner" | Out-Null
Copy-Item "PackageContents.xml" -Destination $staging -Force
Copy-Item "bin\Release\net48-windows\*.dll" -Destination "$staging\Contents\AutomatedClashRunner" -Force

if (Test-Path "en-US") {
    Copy-Item "en-US" -Destination "$staging\Contents\AutomatedClashRunner\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$staging\en-US" -Recurse -Force
}
if (Test-Path "Images") {
    Copy-Item "Images" -Destination "$staging\Contents\AutomatedClashRunner\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$staging\Images" -Recurse -Force
}

$zipDest = "Installer\bundle.zip"
if (Test-Path $zipDest) { Remove-Item $zipDest -Force }
Compress-Archive -Path "$staging\*" -DestinationPath $zipDest -Force

# 3. Build Standalone Installer EXE
Write-Host ">>> 3. Building AutomatedClashRunner_Installer.exe..." -ForegroundColor Cyan
& $msbuild "Installer\Installer.csproj" -p:Configuration=Release -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }

Copy-Item "Installer\bin\Release\AutomatedClashRunner_Installer.exe" -Destination "AutomatedClashRunner_Installer.exe" -Force

# 4. Deploy to local AppData (if Navisworks is not locking it)
Write-Host ">>> 4. Deploying plugin bundle to Navisworks ApplicationPlugins..." -ForegroundColor Cyan
$pluginDir = "$env:APPDATA\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle"
$contentsDir = "$pluginDir\Contents\AutomatedClashRunner"
if (!(Test-Path $contentsDir)) { New-Item -ItemType Directory -Force -Path $contentsDir | Out-Null }
Copy-Item "PackageContents.xml" -Destination $pluginDir -Force

if (Test-Path "en-US") {
    Copy-Item "en-US" -Destination "$contentsDir\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$pluginDir\en-US" -Recurse -Force
}
if (Test-Path "Images") {
    Copy-Item "Images" -Destination "$contentsDir\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$pluginDir\Images" -Recurse -Force
}

try {
    Copy-Item "bin\Release\net48-windows\*.dll" -Destination $contentsDir -Force
    Write-Host " - Plugin DLL(s) deployed to: $contentsDir" -ForegroundColor Green

    # Also deploy to local Program Files if exists
    $pf = "${env:ProgramFiles}\Autodesk\Navisworks Manage 2024\Plugins\AutomatedClashRunner"
    if (Test-Path $pf) {
        Copy-Item "bin\Release\net48-windows\*.dll" -Destination $pf -Force
        if (Test-Path "en-US") { Copy-Item "en-US" -Destination "$pf\en-US" -Recurse -Force }
        if (Test-Path "Images") { Copy-Item "Images" -Destination "$pf\Images" -Recurse -Force }
        Write-Host " - Plugin DLL(s) deployed to Program Files: $pf" -ForegroundColor Green
    }
} catch {
    Write-Host " [WARN] Navisworks is currently running and locking the DLL in AppData." -ForegroundColor Yellow
    Write-Host " [HINT] Close Navisworks and re-run build_all.ps1 or run AutomatedClashRunner_Installer.exe to update." -ForegroundColor Yellow
}

Write-Host "=======================================================" -ForegroundColor Green
Write-Host "BUILD & PACKAGING COMPLETE!" -ForegroundColor Green
Write-Host " - Installer ready at: AutomatedClashRunner_Installer.exe" -ForegroundColor Green
Write-Host "=======================================================" -ForegroundColor Green
