@echo off
REM Runs DiagnosticWPF (Desktop App)

echo Starting DiagnosticWPF...
echo.

REM Start DiagnosticWPF (from repo root)
start "DiagnosticWPF" dotnet run --project DiagExperimentsSolution\DiagnosticWPF

echo.
echo Press any key to exit this launcher window (app will keep running).
pause >nul
