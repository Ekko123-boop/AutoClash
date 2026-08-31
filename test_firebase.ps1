# Quick Firebase Setup, Test & URL Masking Utility for Automated Clash Runner
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
    Write-Host "Testing connection to Firebase endpoint: $endpoint ..." -ForegroundColor Cyan

    try {
        # Check if root autoclash exists
        $existing = $null
        try {
            $existing = Invoke-RestMethod -Uri $endpoint -Method Get
        } catch { }

        if ($null -eq $existing) {
            # Initial setup: create global config and admin machine
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
            Write-Host "[SUCCESS] Initialized new Firebase database schema!" -ForegroundColor Green
        } else {
            # Patch admin machine without wiping existing registered machines
            $adminPatch = @{
                enabled = $true
                user = $env:USERNAME
                machine = $env:COMPUTERNAME
                last_seen = (Get-Date).ToUniversalTime().ToString("o")
                notes = "Admin machine"
            } | ConvertTo-Json

            $patchEndpoint = "$cleanUrl/autoclash/machines/$hwid.json"
            $response = Invoke-RestMethod -Uri $patchEndpoint -Method Patch -Body $adminPatch -ContentType "application/json"
            Write-Host "[SUCCESS] Connected to existing Firebase! Updated admin machine record without overwriting other machines." -ForegroundColor Green
        }

        Write-Host "Admin HWID: $hwid (enabled = true)" -ForegroundColor Green

        # Calculate masked byte array for StringProtection.cs
        $targetEndpoint = "$cleanUrl/autoclash"
        $maskKey = [byte[]]@(
            0x4B, 0x8F, 0x12, 0x99, 0x5C, 0x3E, 0x77, 0xAA,
            0x01, 0xFD, 0x88, 0x34, 0x55, 0x19, 0xEB, 0x72,
            0x39, 0x1A, 0x8F, 0x22, 0x4D, 0x70, 0x88, 0xAC,
            0x29, 0x77, 0x12, 0xEF, 0x50, 0xBB, 0x38, 0x1D
        )
        $urlBytes = [System.Text.Encoding]::UTF8.GetBytes($targetEndpoint)
        for ($i = 0; $i -lt $urlBytes.Length; $i++) {
            $urlBytes[$i] = [byte]($urlBytes[$i] -bxor $maskKey[$i % $maskKey.Length])
        }
        $hexList = $urlBytes | ForEach-Object { '0x{0:X2}' -f $_ }
        $formattedHex = ($hexList -join ', ')

        Write-Host ""
        Write-Host "=========================================================" -ForegroundColor Yellow
        Write-Host "  To embed this Firebase URL into the addin DLL:         " -ForegroundColor Yellow
        Write-Host "=========================================================" -ForegroundColor Yellow
        Write-Host "In Services/StringProtection.cs, set EncryptedEndpoint to:"
        Write-Host $formattedHex -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Then run: .\build_all.ps1" -ForegroundColor Yellow
        Write-Host ""
    } catch {
        Write-Host "[ERROR] Could not connect to Firebase: $_" -ForegroundColor Red
    }
}
