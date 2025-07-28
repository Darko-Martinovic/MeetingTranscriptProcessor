@echo off
echo Starting Meeting Transcript Processor...

echo Starting .NET Web API server...
start /B dotnet run --project . -- --web

echo Waiting for backend to start...
timeout /t 5 /nobreak >nul

echo Starting React frontend...
cd frontend\meeting-transcript-ui
start /B npm run dev

echo.
echo Application started!
echo Backend API: http://localhost:5000
echo Frontend UI: http://localhost:5173
echo.
echo Press any key to exit...
pause >nul
