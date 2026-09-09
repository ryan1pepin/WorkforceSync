@echo off
setlocal
echo ============================================
echo   WorkforceSync - stop local stack
echo ============================================

REM Kill whatever is listening on the three project ports (API, feed, UI).
REM This is the most reliable signal - no process-name or cmdline matching.
powershell -NoProfile -Command "Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 5140,5199,4200 } | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { if ($_ -gt 0) { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue } }"

REM Belt-and-braces: also kill the .NET service processes by name.
powershell -NoProfile -Command "Get-Process WorkforceSync.Api, WorkforceSync.HcmSource -ErrorAction SilentlyContinue | Stop-Process -Force"

echo Stopped.
exit /b 0
