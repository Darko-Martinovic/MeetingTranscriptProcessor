@echo off
title Meeting Transcript Processor Demo

echo ╔══════════════════════════════════════════════════════════════╗
echo ║               Meeting Transcript Processor Demo             ║
echo ╚══════════════════════════════════════════════════════════════╝
echo.

cd /d "%~dp0"

echo 📁 Project Directory: %CD%
echo.

if not exist "data\Incoming" (
    mkdir "data\Incoming"
    echo ✅ Created Incoming directory
)

echo 🔧 Demo Setup Instructions:
echo.
echo 1. OPTIONAL: Configure Azure OpenAI for AI-powered extraction
echo    - Copy .env.example to .env
echo    - Set AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY
echo.
echo 2. OPTIONAL: Configure Jira for actual ticket creation  
echo    - Set JIRA_URL, JIRA_API_TOKEN, JIRA_EMAIL in .env
echo.
echo 3. Without configuration, the app will:
echo    - Use rule-based action item extraction
echo    - Simulate Jira ticket creation
echo.

echo 🚀 To start the demo:
echo.
echo 1. Run the application:
echo    dotnet run
echo.
echo 2. In another terminal, copy the sample file:
echo    copy "data\Templates\sample_meeting_transcript.txt" "data\Incoming\"
echo.
echo 3. Watch the application automatically process the file!
echo.

set /p choice=Would you like to start the application now? (y/N): 

if /i "%choice%"=="y" (
    echo.
    echo 🚀 Starting Meeting Transcript Processor...
    echo    Press Ctrl+C to stop
    echo.
    
    dotnet build --verbosity quiet
    if %errorlevel%==0 (
        dotnet run
    ) else (
        echo ❌ Build failed. Please check the project for errors.
        pause
    )
) else (
    echo.
    echo 📝 Manual start instructions:
    echo    1. Run: dotnet run
    echo    2. Copy sample file to data\Incoming\
    echo    3. Watch the magic happen! ✨
    echo.
    pause
)
