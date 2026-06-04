@echo off
REM Runs DiagnosticServer (ASP.NET Core backend) and uidiag (Vite React frontend) in parallel

echo Starting DiagnosticServer and uidiag in parallel...
echo.

REM Start DiagnosticServer (from repo root)
start "DiagnosticServer" dotnet run --project DiagExperimentsSolution\DiagnosticServer

REM Start uidiag dev server
start "uidiag" /D uidiag npm run dev

echo.
echo Both services launched in separate windows.
echo Press any key to exit this launcher window (services will keep running).
pause >nul
