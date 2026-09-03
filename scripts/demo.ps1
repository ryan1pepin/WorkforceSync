# WorkforceSync demo launcher — one command, fresh state, everything running.
# Usage:  powershell -ExecutionPolicy Bypass -File scripts/demo.ps1
# Stops:  powershell -ExecutionPolicy Bypass -File scripts/demo.ps1 -Stop

param([switch]$Stop)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$apiPort = 5140
$feedPort = 5199
$uiPort = 4200

function Stop-Port([int]$port) {
    $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($conns) {
        $conns | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object {
            Write-Host "  stopping PID $_ on port $port"
            Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
        }
    }
}

if ($Stop) {
    foreach ($p in @($apiPort, $feedPort, $uiPort)) { Stop-Port $p }
    Write-Host "All WorkforceSync processes stopped." -ForegroundColor Green
    exit 0
}

Write-Host "==> Stopping anything already on the demo ports" -ForegroundColor Cyan
foreach ($p in @($apiPort, $feedPort, $uiPort)) { Stop-Port $p }

Write-Host "==> Fresh database (demo starts clean)" -ForegroundColor Cyan
$db = Join-Path $root "WorkforceSync.Api\workforcesync.db"
if (Test-Path $db) { Remove-Item $db -Force }

Write-Host "==> Starting API (port $apiPort, mock HCM feed on $feedPort)" -ForegroundColor Cyan
$api = Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--project", (Join-Path $root "WorkforceSync.Api") `
    -WorkingDirectory $root -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 2

Write-Host "==> Starting Angular dev server (port $uiPort)" -ForegroundColor Cyan
$ui = Start-Process -FilePath "npx" `
    -ArgumentList "ng", "serve", "--port", "$uiPort" `
    -WorkingDirectory (Join-Path $root "frontend") -WindowStyle Hidden -PassThru

Write-Host "==> Waiting for the API to become healthy..." -ForegroundColor Cyan
$healthy = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    try {
        $r = Invoke-RestMethod -Uri "http://localhost:$apiPort/integrations/health" -TimeoutSec 2
        if ($r.status -eq "Healthy") { $healthy = $true; break }
    } catch { }
}

if (-not $healthy) {
    Write-Host "API did not report Healthy in time — check the API console output." -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "  WorkforceSync is up." -ForegroundColor Green
    Write-Host "  Dashboard : http://localhost:$uiPort"
    Write-Host "  API       : http://localhost:$apiPort  (Swagger at /swagger)"
    Write-Host "  HCM feed  : http://localhost:$feedPort/feed"
    Write-Host ""
    Write-Host "  Register any account on the login screen, then sign in."
    Write-Host "  The mock HCM publishes a new workforce event every 15s —"
    Write-Host "  watch the dashboard update live (or set Ingestion:ScenarioStepSeconds lower)."
    Write-Host ""
    Write-Host "  Stop everything:  .\scripts\demo.ps1 -Stop"
    Write-Host ""
}

Start-Process "http://localhost:$uiPort"
