# =====================================================================
# Rimo Tools - Full End-to-End Build & Deployment Script
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

# 2. Build Multi-Version Plugin DLLs
Write-Host ">>> 1. Building Navisworks 2023 Engine (Release2023)..." -ForegroundColor Cyan
& $msbuild "AutomatedClashRunner.csproj" -p:Configuration=Release2023 -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "2023 Plugin build failed." }

Write-Host ">>> 2. Building Navisworks 2024 Engine (Release2024)..." -ForegroundColor Cyan
& $msbuild "AutomatedClashRunner.csproj" -p:Configuration=Release2024 -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "2024 Plugin build failed." }

# 3. Direct AppData Deployment: Multi-Version ApplicationPlugins Bundle
Write-Host ">>> 3. Deploying Multi-Version RimoNavisTools.bundle to Navisworks ApplicationPlugins..." -ForegroundColor Cyan
$bundleDir = "$env:APPDATA\Autodesk\ApplicationPlugins\RimoNavisTools.bundle"
if (Test-Path $bundleDir) { Remove-Item $bundleDir -Recurse -Force }

$contentsDir2023 = "$bundleDir\Contents\2023"
$contentsDir2024 = "$bundleDir\Contents\2024"
New-Item -ItemType Directory -Force -Path $contentsDir2023 | Out-Null
New-Item -ItemType Directory -Force -Path $contentsDir2024 | Out-Null

Copy-Item "PackageContents.xml" -Destination $bundleDir -Force
Copy-Item "bin\Release\2023\*.dll" -Destination $contentsDir2023 -Force
Copy-Item "bin\Release\2024\*.dll" -Destination $contentsDir2024 -Force

if (Test-Path "en-US") {
    Copy-Item "en-US" -Destination "$contentsDir2023\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$contentsDir2024\en-US" -Recurse -Force
    Copy-Item "en-US" -Destination "$bundleDir\en-US" -Recurse -Force
}
if (Test-Path "Images") {
    Copy-Item "Images" -Destination "$contentsDir2023\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$contentsDir2024\Images" -Recurse -Force
    Copy-Item "Images" -Destination "$bundleDir\Images" -Recurse -Force
}
Get-ChildItem $bundleDir -Recurse | Unblock-File -ErrorAction SilentlyContinue
Write-Host " - Multi-Version Bundle deployed to: $bundleDir" -ForegroundColor Green

# 4. Direct AppData Deployment: Navisworks Manage 2024 User Plugins Directory
Write-Host ">>> 4. Deploying to Navisworks Manage 2024 User Plugins Directory..." -ForegroundColor Cyan
$userPluginsDir = "$env:APPDATA\Autodesk\Navisworks Manage 2024\Plugins\RimoNavisTools"
if (Test-Path $userPluginsDir) { Remove-Item $userPluginsDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $userPluginsDir | Out-Null
Copy-Item "bin\Release\2024\*.dll" -Destination $userPluginsDir -Force
if (Test-Path "en-US") { Copy-Item "en-US" -Destination "$userPluginsDir\en-US" -Recurse -Force }
if (Test-Path "Images") { Copy-Item "Images" -Destination "$userPluginsDir\Images" -Recurse -Force }
Get-ChildItem $userPluginsDir -Recurse | Unblock-File -ErrorAction SilentlyContinue
Write-Host " - User Plugin deployed to: $userPluginsDir" -ForegroundColor Green

Write-Host "====================================================================" -ForegroundColor Green
Write-Host "MULTI-VERSION BUILD & PACKAGING COMPLETE (2020-2026 READY)!" -ForegroundColor Green
Write-Host " - 1-Click Multi-Version Installer ready at: Install_RimoTools.bat" -ForegroundColor Green
Write-Host "====================================================================" -ForegroundColor Green

