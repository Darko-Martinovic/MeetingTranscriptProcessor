# Meeting Transcript Processor

An intelligent web application that automatically processes meeting transcripts and creates Jira tickets from extracted action items using Azure OpenAI.

## ✨ Key Features

- 🤖 **AI-Powered Extraction** - Uses Azure OpenAI with advanced validation (hallucination detection, consistency management)
- 🎫 **Jira Integration** - Automatically creates tickets from action items
- � **Modern Web UI** - React frontend with folder management, filtering, and batch upload
- � **Multi-format Support** - Processes .txt, .md, .json, .xml, .docx, and .pdf files
- � **Fallback Mode** - Works without AI credentials using rule-based extraction
- 🎯 **Context-Aware** - Adapts to meeting types (standup, sprint, incident, etc.) and languages (EN/FR/NL)

## 🎬 Demo Video

See the Meeting Transcript Processor in action:

https://github.com/user-attachments/assets/cb08feaa-05fa-4988-abfb-038d2ebb02d9

_Watch how the application automatically processes meeting transcripts, extracts action items using Azure OpenAI, and creates Jira tickets seamlessly._

## 🚀 Quick Start

### Prerequisites

- .NET 9.0 SDK
- Node.js 18+ and npm
- Azure OpenAI account (optional)
- Jira account with API access (optional)

### Installation & Setup

1. **Clone and restore dependencies:**

   ```powershell
   git clone <repository-url>
   cd MeetingTranscriptProcessor
   dotnet restore
   ```

2. **Configure environment (optional):**

   ```powershell
   Copy-Item .env.example .env
   # Edit .env with your Azure OpenAI and Jira credentials
   ```

3. **Run the application:**

   **Backend:**

   ```powershell
   dotnet run --web
   ```

   **Frontend:**

   ```powershell
   cd frontend/meeting-transcript-ui
   npm install
   npm run dev
   ```

4. **Access the application:**
   - Frontend: http://localhost:5173
   - Backend API: http://localhost:5000

## 📁 How It Works

```
📤 Upload → � Processing → 🤖 AI Analysis → 🎫 Jira Tickets → 📦 Archive
```

1. Upload transcript files via web interface
2. Files automatically move through Incoming → Processing → Archive
3. AI extracts action items with validation
4. Jira tickets created automatically
5. View results in the Archive folder with filtering

## ⚙️ Configuration

All settings are **optional** - the app works in simulation mode without credentials.

### Environment Variables (.env)

```env
# Azure OpenAI (Optional - enables AI extraction)
AOAI_ENDPOINT=https://your-resource.openai.azure.com/
AOAI_APIKEY=your-api-key
CHATCOMPLETION_DEPLOYMENTNAME=gpt-35-turbo

# Jira (Optional - enables actual ticket creation)
JIRA_URL=https://your-domain.atlassian.net
JIRA_API_TOKEN=your-api-token
JIRA_EMAIL=your-email@company.com
JIRA_PROJECT_KEY=TASK

# AI Validation (Optional)
ENABLE_VALIDATION=true
ENABLE_HALLUCINATION_DETECTION=true
ENABLE_CONSISTENCY_MANAGEMENT=true
```

### Operation Modes

- **🤖 Full AI Mode**: Azure OpenAI + Jira (recommended)
- **🔧 Hybrid Mode**: Either Azure OpenAI OR Jira
- **⚙️ Offline Mode**: No credentials needed, rule-based extraction

## 🏗️ Architecture

### Frontend (React + TypeScript)

- Modern React 18 with TypeScript
- Vite for fast development
- CSS Modules for styling
- Responsive design with mobile support

### Backend (.NET 9)

- ASP.NET Core Web API
- Background file processing services
- Advanced AI validation pipeline
- RESTful API endpoints

### AI/ML Validation Services

- **HallucinationDetector**: 6-step validation preventing false positives
- **ConsistencyManager**: Context-aware extraction for 8 meeting types + 3 languages
- **ActionItemValidator**: 4-technique validation with weighted scoring

## � Sample Files & Usage

### Supported Formats

- Text files (.txt, .md)
- JSON/XML (.json, .xml)
- Documents (.docx, .pdf)

### Sample Transcript

```text
Meeting: Sprint Planning - Q1 2025
Date: 2025-01-15
Participants: Alice, Bob, Carol

Alice: We need to implement user authentication by Friday.
Bob: I'll fix the login bug and create API documentation.
Carol: Let's schedule the UI review meeting.

Action Items:
- Alice: Implement authentication (Due: Friday)
- Bob: Fix login bug, create docs (Due: This week)
- Carol: Schedule UI review meeting
```

## 🛠️ Development

### Build & Test

```powershell
# Backend
dotnet build
dotnet test
dotnet watch run --web

# Frontend
cd frontend/meeting-transcript-ui
npm install
npm run build
npm run dev
```

### Project Structure

```
MeetingTranscriptProcessor/
├── Controllers/           # Web API
├── Services/             # Business logic & AI validation
├── Models/               # Data contracts
├── frontend/            # React application
├── data/                # File storage
└── .env.example         # Configuration template
```

## 📖 API Endpoints

- **GET** `/api/meetings/folders` - Get folders with meeting counts
- **GET** `/api/meetings/folders/{type}/meetings` - Get meetings with filtering
- **POST** `/api/meetings/upload` - Upload transcript files
- **GET** `/api/configuration` - Get current settings
- **PUT** `/api/configuration/azure-openai` - Update AI settings
- **PUT** `/api/configuration/jira` - Update Jira settings
- **GET** `/api/status` - System health and metrics

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License.

---

**Perfect for:** Teams wanting to automate action item tracking from meeting transcripts with enterprise-grade AI validation and Jira integration.
