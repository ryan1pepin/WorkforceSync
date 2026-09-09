@echo off
setlocal
cd /d "%~dp0"

echo ============================================
echo   WorkforceSync - reset (clean DB + restart)
echo ============================================

echo [1/3] Stopping existing instances...
call "%~dp0stop.bat"
echo.

echo [2/3] Clearing database...
timeout /t 2 /nobreak >nul
del /f /q "%~dp0WorkforceSync.Api\workforcesync.db" >nul 2>&1
del /f /q "%~dp0WorkforceSync.Api\workforcesync.db-wal" >nul 2>&1
del /f /q "%~dp0WorkforceSync.Api\workforcesync.db-shm" >nul 2>&1
echo Database cleared.
echo.

echo [3/3] Starting fresh...
call "%~dp0start.bat"
