@echo off
REM Runs TestWebApp and StressTestWebApp in parallel

echo Starting TestWebApp and StressTestWebApp in parallel...
echo.

REM Start TestWebApp
start "TestWebApp" dotnet run --project DiagExperimentsSolution\TestWebApp

REM Start StressTestWebApp
start "StressTestWebApp" dotnet run --project DiagExperimentsSolution\StressTestWebApp

echo.
echo Both test apps launched in separate windows.
echo Press any key to exit this launcher window (apps will keep running).
pause >nul
