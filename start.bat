@echo off
setlocal
cd /d "%~dp0"

echo ============================================
echo   WorkforceSync - start local stack
echo ============================================
echo.

echo [1/4] Stopping any existing instances...
call "%~dp0stop.bat"
echo.

echo [2/4] Building backend...
dotnet build WorkforceSync.slnx -v q
if errorlevel 1 (
    echo.
    echo BUILD FAILED - see output above. Aborting.
    exit /b 1
)
echo.

echo [3/4] Starting services (3 windows will open)...
start "WorkforceSync API" /D "%~dp0WorkforceSync.Api" cmd /k dotnet run --no-build
start "WorkforceSync HCM Feed" /D "%~dp0WorkforceSync.HcmSource" cmd /k dotnet run --no-build
start "WorkforceSync UI" /D "%~dp0frontend" cmd /k npx ng serve --port 4200
echo.

echo ============================================
echo   Stack launched
echo     UI     http://localhost:4200
echo     API    http://localhost:5140/swagger
echo     Feed   http://localhost:5199/feed
echo   Login: demo@corp.example / Demo123!
echo   Stop:   stop.bat      Reset DB: reset.bat
echo ============================================
exit /b 0
