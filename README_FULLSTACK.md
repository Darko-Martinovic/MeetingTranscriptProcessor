# Meeting Transcript Processor - Full Stack Application

A comprehensive solution for processing meeting transcripts with both console and web interfaces. The application automatically extracts action items and creates Jira tickets using AI-powered analysis.

## 🚀 Quick Start

### Prerequisites
- .NET 9.0 SDK
- Node.js 18+ and npm
- Azure OpenAI account (optional)
- Jira account (optional)

### Easy Setup
1. **Clone and setup**:
   ```bash
   git clone <repository-url>
   cd MeetingTranscriptProcessor
   ```

2. **Configure environment** (copy `.env.example` to `.env` and fill in your values):
   ```env
   # Azure OpenAI (optional - fallback to rule-based extraction)
   AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
   AZURE_OPENAI_API_KEY=your-api-key
   AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4

   # Jira Integration (optional - simulation mode if not configured)
   JIRA_URL=https://your-domain.atlassian.net
   JIRA_API_TOKEN=your-api-token
   JIRA_EMAIL=your-email@example.com
   JIRA_PROJECT_KEY=OPS

   # Application Settings
   MAX_CONCURRENT_FILES=3
   ENABLE_VALIDATION=true
   ENABLE_HALLUCINATION_DETECTION=true
   ENABLE_CONSISTENCY_MANAGEMENT=true
   VALIDATION_CONFIDENCE_THRESHOLD=0.5
   ```

3. **Run the full-stack application**:
   
   **Windows (PowerShell)**:
   ```powershell
   .\start-full-app.ps1
   ```
   
   **Windows (Command Prompt)**:
   ```cmd
   start-full-app.bat
   ```
   
   **Manual Setup**:
   ```bash
   # Terminal 1: Backend API
   dotnet run -- --web
   
   # Terminal 2: Frontend (in new terminal)
   cd frontend/meeting-transcript-ui
   npm install
   npm run dev
   ```

4. **Access the application**:
   - **Web UI**: http://localhost:5173
   - **Backend API**: http://localhost:5000
   - **Console Mode**: `dotnet run` (without --web flag)

## 🎯 Features

### Web Interface
- **📁 Folder Explorer**: Browse Archive, Incoming, and Processing folders
- **📄 Meeting Viewer**: View detailed meeting transcripts and extracted action items
- **⬆️ File Upload**: Drag-and-drop or click to upload meeting files
- **⭐ Favorites**: Star important meetings for quick access
- **🕒 Recent Meetings**: Quick access to recently viewed meetings
- **⚙️ Configuration**: Update Azure OpenAI, Jira, and extraction settings
- **📊 Real-time Status**: Live system status and processing indicators
- **💾 Local Storage**: No external database required - uses browser localStorage

### Backend Capabilities
- **🤖 AI-Powered Extraction**: Azure OpenAI integration for intelligent action item detection
- **📝 Rule-Based Fallback**: Works without AI configuration
- **🎫 Jira Integration**: Automatic ticket creation and updates
- **🔍 Advanced Validation**: Hallucination detection and consistency management
- **⚡ Concurrent Processing**: Process multiple files simultaneously
- **🌐 Multi-language Support**: English, Spanish, French, German, Portuguese
- **📁 Auto File Management**: Automatic archiving with status tracking

### Console Interface
- **📂 File Monitoring**: Watch directories for new transcript files
- **⌨️ Interactive Commands**: status, metrics, help, quit
- **📊 Validation Metrics**: AI/ML reliability monitoring
- **🧹 Graceful Shutdown**: Clean resource cleanup

## 📋 Supported File Formats
- `.txt` - Plain text transcripts
- `.md` - Markdown formatted transcripts
- `.json` - JSON structured transcripts
- `.xml` - XML formatted transcripts
- `.docx` - Microsoft Word documents
- `.pdf` - PDF documents

## 🛠️ Configuration Options

### Web UI Configuration
Access the settings modal in the web interface to configure:

1. **Azure OpenAI Settings**:
   - Endpoint URL
   - API Key
   - Deployment Name

2. **Extraction Settings**:
   - Max Concurrent Files (1-10)
   - Validation Confidence Threshold (0.0-1.0)
   - Enable/Disable Validation Features

3. **Jira Settings**:
   - Jira URL
   - Email
   - API Token
   - Default Project Key

### Environment Variables
All settings can also be configured via environment variables (see `.env.example`).

## 🏗️ Architecture

### Backend (.NET 9)
- **ASP.NET Core Web API**: RESTful endpoints for frontend
- **Background Services**: File monitoring and processing
- **Dependency Injection**: Clean, testable architecture
- **Hybrid Mode**: Console or Web API modes

### Frontend (React + TypeScript)
- **Vite**: Fast build tool and dev server
- **TypeScript**: Type-safe development
- **Tailwind CSS**: Utility-first styling
- **Lucide React**: Beautiful icons
- **Axios**: HTTP client for API calls

### File System Structure
```
Data/
├── Incoming/     # Drop new transcript files here
├── Processing/   # Files currently being processed
└── Archive/      # Completed files with status/language info
```

## 🎨 UI Features

### Modern Interface
- **Responsive Design**: Works on desktop, tablet, and mobile
- **Clean Aesthetics**: Professional, easy-to-use interface
- **Real-time Updates**: Auto-refresh every 30 seconds
- **Error Handling**: User-friendly error messages
- **Loading States**: Clear feedback during operations

### File Management
- **Visual File Browser**: See file sizes, dates, and status
- **Status Indicators**: Success, error, processing states
- **Preview Content**: Quick preview of transcript content
- **Bulk Operations**: Multiple file handling

### Meeting Details
- **Structured View**: Organized display of meeting information
- **Participant Lists**: Clear participant visualization
- **Action Items**: Detailed action item breakdown with assignees and priorities
- **Content Display**: Scrollable, formatted transcript content

## 🔧 Development

### Backend Development
```bash
# Build and run backend
dotnet build
dotnet run                  # Console mode
dotnet run -- --web       # Web API mode

# Run tests
dotnet test

# Watch for changes
dotnet watch run -- --web
```

### Frontend Development
```bash
cd frontend/meeting-transcript-ui

# Install dependencies
npm install

# Development server
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview
```

### API Endpoints
- `GET /api/meetings/folders` - Get folder information
- `GET /api/meetings/folders/{type}/meetings` - Get meetings in folder
- `GET /api/meetings/meeting/{filename}` - Get meeting details
- `POST /api/meetings/upload` - Upload new meeting file
- `DELETE /api/meetings/meeting/{filename}` - Delete meeting file
- `GET /api/configuration` - Get configuration settings
- `PUT /api/configuration/{section}` - Update configuration
- `GET /api/configuration/system-status` - Get system status

## 🚨 Troubleshooting

### Common Issues

1. **Backend won't start**:
   - Check .NET 9.0 SDK is installed: `dotnet --version`
   - Verify port 5000 is available
   - Check environment variables in `.env`

2. **Frontend won't start**:
   - Check Node.js version: `node --version` (requires 18+)
   - Install dependencies: `npm install`
   - Clear npm cache: `npm cache clean --force`

3. **API connection errors**:
   - Ensure backend is running on http://localhost:5000
   - Check browser console for CORS errors
   - Verify firewall settings

4. **File processing issues**:
   - Check file permissions in Data directories
   - Verify supported file formats
   - Check Azure OpenAI configuration if using AI

### Debug Mode
Run with detailed logging:
```bash
# Backend with debug logging
ASPNETCORE_ENVIRONMENT=Development dotnet run -- --web

# Frontend with network debugging
npm run dev -- --debug
```

## 📄 License
[Your License Here]

## 🤝 Contributing
[Your Contribution Guidelines Here]

## 📧 Support
[Your Support Information Here]

---

**Enjoy processing your meeting transcripts with ease! 🎉**
