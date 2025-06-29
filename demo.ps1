# Meeting Transcript Processor - Demo Script
# This script demonstrates the application functionality

Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║               Meeting Transcript Processor Demo             ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Set working directory
$projectDir = Split-Path -Path $MyInvocation.MyCommand.Path -Parent
Set-Location $projectDir

Write-Host "📁 Project Directory: $projectDir" -ForegroundColor Green
Write-Host ""

# Check if sample transcript exists
$sampleFile = "data\Templates\sample_meeting_transcript.txt"
$incomingDir = "data\Incoming"

# Ensure directories exist
if (-not (Test-Path $incomingDir)) {
    New-Item -Path $incomingDir -ItemType Directory -Force | Out-Null
    Write-Host "✅ Created Incoming directory" -ForegroundColor Green
}

Write-Host "🔧 Demo Setup Instructions:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. OPTIONAL: Configure Azure OpenAI (for AI-powered extraction)" -ForegroundColor White
Write-Host "   - Copy .env.example to .env" -ForegroundColor Gray
Write-Host "   - Set AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY" -ForegroundColor Gray
Write-Host ""
Write-Host "2. OPTIONAL: Configure Jira (for actual ticket creation)" -ForegroundColor White
Write-Host "   - Set JIRA_URL, JIRA_API_TOKEN, JIRA_EMAIL in .env" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Without configuration, the app will:" -ForegroundColor White
Write-Host "   - Use rule-based action item extraction" -ForegroundColor Gray
Write-Host "   - Simulate Jira ticket creation" -ForegroundColor Gray
Write-Host ""

Write-Host "🚀 To start the demo:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Run the application:" -ForegroundColor White
Write-Host "   dotnet run" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. In another terminal, copy your meeting files:" -ForegroundColor White
Write-Host "   Copy-Item 'd:\Users\Darko\Downloads\meeting_transcript_*.txt' 'Data\Incoming\'" -ForegroundColor Cyan
Write-Host "   (Or they're already there if you copied them manually)" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Watch the application automatically process the file!" -ForegroundColor White
Write-Host ""

Write-Host "📋 Real Meeting Action Items to be extracted:" -ForegroundColor Yellow
Write-Host "   Meeting 1 (PLU Data Analysis):" -ForegroundColor White
Write-Host "   • Analyze PLU JSON data from GK API integration (Darko & Nikola - Due: 1 week)" -ForegroundColor Gray
Write-Host ""
Write-Host "   Meeting 2 (Critical Bug Fix):" -ForegroundColor White
Write-Host "   • URGENT: Fix Promo Search stored procedure (Aidil - Critical Priority)" -ForegroundColor Gray
Write-Host ""
Write-Host "   Meeting 3 (Documentation Update):" -ForegroundColor White
Write-Host "   • Update Confluence deployment template with T-SQL docs (Valery - Due: End of week)" -ForegroundColor Gray
Write-Host ""

Write-Host "🎫 Expected Jira Tickets (OPS project):" -ForegroundColor Yellow
Write-Host "   OPS-XXXX: Analyze PLU JSON data from GK API integration" -ForegroundColor Gray
Write-Host "   OPS-XXXX: URGENT: Fix Promo Search stored procedure returning incorrect results" -ForegroundColor Gray
Write-Host "   OPS-XXXX: Update Confluence production deployment template with enhanced T-SQL documentation" -ForegroundColor Gray
Write-Host ""

$choice = Read-Host "Would you like to start the application now? (y/N)"
if ($choice -eq 'y' -or $choice -eq 'Y') {
    Write-Host ""
    Write-Host "🚀 Starting Meeting Transcript Processor..." -ForegroundColor Green
    Write-Host "   (Press Ctrl+C to stop)" -ForegroundColor Gray
    Write-Host ""
    
    # Build and run the application
    dotnet build --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        dotnet run
    } else {
        Write-Host "❌ Build failed. Please check the project for errors." -ForegroundColor Red
    }
} else {
    Write-Host ""
    Write-Host "📝 Manual start instructions:" -ForegroundColor Yellow
    Write-Host "   1. Run: dotnet run" -ForegroundColor Cyan
    Write-Host "   2. Meeting transcripts are already in Data\Incoming\" -ForegroundColor Cyan
    Write-Host "   3. Watch the magic happen! ✨" -ForegroundColor Cyan
    Write-Host ""
}
