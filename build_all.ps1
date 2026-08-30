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

# 2. Deploy to local AppData
Write-Host ">>> 2. Deploying plugin bundle to Navisworks ApplicationPlugins..." -ForegroundColor Cyan
$pluginDir = "$env:APPDATA\Autodesk\ApplicationPlugins\AutomatedClashRunner.bundle"
$contentsDir = "$pluginDir\Contents"
if (!(Test-Path $contentsDir)) { New-Item -ItemType Directory -Force -Path $contentsDir | Out-Null }
Copy-Item "PackageContents.xml" -Destination $pluginDir -Force
Copy-Item "bin\Release\net48-windows\AutomatedClashRunner.dll" -Destination $contentsDir -Force

# 3. Create bundle.zip for Installer
Write-Host ">>> 3. Packaging bundle.zip..." -ForegroundColor Cyan
$staging = "$env:TEMP\acr_bundle_staging"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path "$staging\Contents" | Out-Null
Copy-Item "PackageContents.xml" -Destination $staging -Force
Copy-Item "bin\Release\net48-windows\AutomatedClashRunner.dll" -Destination "$staging\Contents" -Force

$zipDest = "Installer\bundle.zip"
if (Test-Path $zipDest) { Remove-Item $zipDest -Force }
Compress-Archive -Path "$staging\*" -DestinationPath $zipDest -Force

# 4. Build Standalone Installer EXE
Write-Host ">>> 4. Building AutomatedClashRunner_Installer.exe..." -ForegroundColor Cyan
& $msbuild "Installer\Installer.csproj" -p:Configuration=Release -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }

Copy-Item "Installer\bin\Release\AutomatedClashRunner_Installer.exe" -Destination "AutomatedClashRunner_Installer.exe" -Force

Write-Host "=======================================================" -ForegroundColor Green
Write-Host "BUILD & DEPLOYMENT COMPLETE!" -ForegroundColor Green
Write-Host " - Plugin deployed to: $pluginDir" -ForegroundColor Green
Write-Host " - Installer ready at: AutomatedClashRunner_Installer.exe" -ForegroundColor Green
Write-Host "=======================================================" -ForegroundColor Green
