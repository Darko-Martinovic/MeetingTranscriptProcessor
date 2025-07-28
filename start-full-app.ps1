# Meeting Transcript Processor - Startup Script

Write-Host "Starting Meeting Transcript Processor..." -ForegroundColor Green

# Start the .NET Web API in the background
Write-Host "Starting .NET Web API server..." -ForegroundColor Yellow
$backendProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", ".", "--", "--web" -WindowStyle Hidden -PassThru

# Wait a moment for the backend to start
Start-Sleep -Seconds 5

# Start the React frontend
Write-Host "Starting React frontend..." -ForegroundColor Yellow
Set-Location "frontend\meeting-transcript-ui"
Start-Process -FilePath "npm" -ArgumentList "run", "dev" -NoNewWindow

Write-Host "Application started!" -ForegroundColor Green
Write-Host "Backend API: http://localhost:5000" -ForegroundColor Cyan
Write-Host "Frontend UI: http://localhost:5173" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press any key to stop all services..." -ForegroundColor Yellow

# Wait for user input
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Stop the backend process
if ($backendProcess -and !$backendProcess.HasExited) {
    Write-Host "Stopping backend server..." -ForegroundColor Yellow
    Stop-Process -Id $backendProcess.Id -Force
}

Write-Host "Services stopped." -ForegroundColor Green
