# Meeting Transcript Processor - Full Stack Application

This application now includes both a powerful console/API backend and a modern React TypeScript frontend.

## Features

### Backend (Console + Web API)
- **Dual Mode Operation**: Run as console app OR web API
- **File Processing**: Automatic processing of meeting transcripts
- **AI Integration**: Azure OpenAI for intelligent action item extraction
- **Jira Integration**: Automatic ticket creation and management
- **Multi-language Support**: English, French, Dutch, Spanish, German, Portuguese
- **Advanced Validation**: Hallucination detection and consistency management
- **Concurrent Processing**: Handle multiple files simultaneously

### Frontend (React + TypeScript)
- **Folder Explorer**: Browse Archive, Incoming, and Processing folders
- **Meeting Viewer**: Detailed view of processed meetings with action items
- **File Upload**: Drag & drop support for meeting files
- **Configuration Management**: Update Azure OpenAI, Jira, and processing settings
- **Favorites & Recent**: Quick access to frequently viewed meetings
- **Real-time Status**: Live system status and auto-refresh
- **Local Storage**: No external database required

## Quick Start

### Option 1: Use the Startup Scripts
```powershell
# Windows PowerShell
.\start-full-app.ps1

# OR Windows Command Prompt
start-full-app.bat
```

### Option 2: Manual Startup

1. **Start the Backend (Web API mode)**:
   ```bash
   dotnet run -- --web
   ```
   - Backend API will be available at: http://localhost:5000

2. **Start the Frontend**:
   ```bash
   cd frontend/meeting-transcript-ui
   npm run dev
   ```
   - Frontend UI will be available at: http://localhost:5173

## Configuration

The frontend provides a settings panel to configure:

### Azure OpenAI Settings
- Endpoint URL
- API Key
- Deployment Name

### Extraction Settings
- Max Concurrent Files (1-10)
- Validation Confidence Threshold (0.0-1.0)
- Enable/Disable Validation Features
- Enable/Disable Hallucination Detection
- Enable/Disable Consistency Management

### Jira Settings
- Jira URL
- Email
- API Token
- Default Project

## API Endpoints

### Meetings API
- `GET /api/meetings/folders` - Get all folders with meeting counts
- `GET /api/meetings/folders/{folderType}/meetings` - Get meetings in a folder
- `GET /api/meetings/meeting/{fileName}` - Get meeting details
- `POST /api/meetings/upload` - Upload a new meeting file
- `DELETE /api/meetings/meeting/{fileName}` - Delete a meeting file

### Configuration API
- `GET /api/configuration` - Get current configuration
- `PUT /api/configuration/azure-openai` - Update Azure OpenAI settings
- `PUT /api/configuration/extraction` - Update extraction settings
- `PUT /api/configuration/jira` - Update Jira settings
- `GET /api/configuration/system-status` - Get system status

## File Support

Supported meeting file formats:
- .txt (Plain text)
- .md (Markdown)
- .json (JSON format)
- .xml (XML format)
- .docx (Word documents)
- .pdf (PDF documents)

## Folder Structure

- **Incoming/**: Drop new meeting files here for processing
- **Processing/**: Files currently being processed
- **Archive/**: Successfully processed files with timestamps and status

## Frontend Features in Detail

### Folder Explorer
- Real-time folder browsing
- Meeting count indicators
- Status-based organization

### Meeting Viewer
- Full meeting content display
- Extracted action items with details
- Participant lists
- Meeting metadata

### File Upload
- Drag and drop interface
- File type validation
- Progress feedback

### Configuration Panel
- Three-tab interface for different settings
- Form validation
- Real-time feedback

### Local Storage Features
- Favorites list (persistent)
- Recent meetings (last 10)
- User preferences

## Development

### Backend
- Built with .NET 9.0
- ASP.NET Core Web API
- Dependency injection
- Background file processing

### Frontend
- React 18 + TypeScript
- Vite build tool
- Tailwind CSS for styling
- Lucide React for icons
- Axios for HTTP requests

## Console Mode Commands

When running in console mode (without `--web` flag):
- `status` or `s` - Show system status
- `metrics` or `m` - Show AI validation metrics
- `help` or `h` - Show help
- `quit` or `q` - Exit application

## Architecture

```
Meeting Transcript Processor
├── Backend (.NET Console/Web API)
│   ├── Controllers/
│   │   ├── MeetingsController.cs
│   │   └── ConfigurationController.cs
│   ├── Services/
│   ├── Models/
│   └── Program.cs / HybridProgram.cs
└── Frontend (React TypeScript)
    ├── src/
    │   ├── services/api.ts
    │   ├── App.tsx
    │   └── index.css
    └── package.json
```

## Environment Variables

Configure via .env file or system environment:

```env
# Azure OpenAI
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4

# Jira
JIRA_URL=https://your-domain.atlassian.net
JIRA_EMAIL=your-email@example.com
JIRA_API_TOKEN=your-api-token
JIRA_DEFAULT_PROJECT=TASK

# Processing
MAX_CONCURRENT_FILES=3
ENABLE_VALIDATION=true
ENABLE_HALLUCINATION_DETECTION=true
ENABLE_CONSISTENCY_MANAGEMENT=true
VALIDATION_CONFIDENCE_THRESHOLD=0.5

# Directories (optional)
INCOMING_DIRECTORY=Data\Incoming
PROCESSING_DIRECTORY=Data\Processing
ARCHIVE_DIRECTORY=Data\Archive

# Web API Mode
RUN_AS_WEB_API=true
```

## Troubleshooting

1. **Backend won't start**: Check that port 5000 is available
2. **Frontend API errors**: Ensure backend is running first
3. **File upload fails**: Check file format and size limits
4. **Configuration not saving**: Check API connectivity

## Next Steps

This implementation provides a solid foundation that can be extended with:
- User authentication
- Real-time WebSocket updates
- Advanced search and filtering
- Export functionality
- Mobile responsiveness improvements
- Dark/light theme toggle
