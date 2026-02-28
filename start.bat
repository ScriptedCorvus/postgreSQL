@echo off
setlocal

cd /d "%~dp0"

echo [1/3] Closing running DatabaseClient.App process (if any)...
taskkill /F /IM DatabaseClient.App.exe >nul 2>&1

echo [2/3] Building solution...
dotnet build "DatabaseClient.slnx"
if errorlevel 1 (
    echo.
    echo Build failed. Aborting run.
    exit /b 1
)

echo [3/3] Starting application...
dotnet run --project "src\DatabaseClient.App"

endlocal
