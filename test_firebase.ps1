# Quick Firebase Setup & Verification Utility for Automated Clash Runner
param(
    [string]$FirebaseUrl = ""
)

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "  Automated Clash Runner - Remote Kill-Switch Tester" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host ""

$bytes = [System.IO.File]::ReadAllBytes('.\bin\Release\net48-windows\AutomatedClashRunner.dll')
$asm = [System.Reflection.Assembly]::Load($bytes)
$hwidType = $asm.GetType('AutomatedClashRunner.Services.HardwareFingerprint')
$hwid = $hwidType.GetMethod('GetMachineId').Invoke($null, $null)
$details = $hwidType.GetMethod('GetMachineDetails').Invoke($null, $null)

Write-Host "Your Machine Hardware ID (HWID): " -NoNewline
Write-Host $hwid -ForegroundColor Yellow
Write-Host "Machine Details:                 $details"
Write-Host ""

if ([string]::IsNullOrWhiteSpace($FirebaseUrl)) {
    Write-Host "To connect your own Firebase Realtime Database:" -ForegroundColor Green
    Write-Host "1. Go to https://console.firebase.google.com and create a free project."
    Write-Host "2. Go to 'Build' -> 'Realtime Database' -> 'Create Database'."
    Write-Host "3. In Rules tab, set: { 'rules': { '.read': true, '.write': true } }"
    Write-Host "4. Copy your database URL (e.g., https://your-project-default-rtdb.firebaseio.com)."
    Write-Host "5. Run this script: .\test_firebase.ps1 -FirebaseUrl 'https://your-project-default-rtdb.firebaseio.com'"
    Write-Host ""
} else {
    $cleanUrl = $FirebaseUrl.TrimEnd('/')
    $endpoint = "$cleanUrl/autoclash.json"
    Write-Host "Checking Firebase endpoint: $endpoint ..." -ForegroundColor Cyan

    try {
        $initData = @{
            global_kill = $false
            lease_days = 14
            kill_message = "Automated Clash Runner has been disabled by the administrator."
            machines = @{
                $hwid = @{
                    enabled = $true
                    user = $env:USERNAME
                    machine = $env:COMPUTERNAME
                    first_seen = (Get-Date).ToUniversalTime().ToString("o")
                    last_seen = (Get-Date).ToUniversalTime().ToString("o")
                    notes = "Admin machine"
                }
            }
        } | ConvertTo-Json -Depth 5

        $response = Invoke-RestMethod -Uri $endpoint -Method Put -Body $initData -ContentType "application/json"
        Write-Host "[SUCCESS] Connected to Firebase and initialized schema!" -ForegroundColor Green
        Write-Host "Registered HWID: $hwid with enabled = true" -ForegroundColor Green
    } catch {
        Write-Host "[ERROR] Could not connect to Firebase: $_" -ForegroundColor Red
    }
}
