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

echo [3/4] Starting services (2 windows will open)...
start "WorkforceSync API" /D "%~dp0WorkforceSync.Api" cmd /k dotnet run --no-build
start "WorkforceSync UI" /D "%~dp0frontend" cmd /k npx ng serve --port 4200
echo.

echo [4/4] Waiting for the API + UI, then opening your browser...
set "tries=0"
:waitloop
powershell -NoProfile -Command "try { $a=New-Object Net.Sockets.TcpClient; $a.Connect('127.0.0.1',5140); $a.Close(); $b=New-Object Net.Sockets.TcpClient; $b.Connect('127.0.0.1',4200); $b.Close(); exit 0 } catch { exit 1 }" >NUL 2>&1
if not errorlevel 1 goto openbrowser
set /a tries+=1
if %tries% GEQ 60 (
    echo Services not ready yet - opening the browser anyway (refresh if it's blank).
    goto openbrowser
)
timeout /t 1 /nobreak >NUL
goto waitloop

:openbrowser
start "" http://localhost:4200
echo.

echo ============================================
echo   Stack launched
echo     UI     http://localhost:4200   (browser opened)
echo     API    http://localhost:5140/swagger
echo     Feed   http://localhost:5199/feed
echo   Login: demo@corp.example / Demo123!
echo   Stop:   stop.bat      Reset DB: reset.bat
echo ============================================
exit /b 0
