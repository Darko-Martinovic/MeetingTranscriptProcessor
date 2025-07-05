# Meeting Transcript Processor

An intelligent application that automatically processes meeting transcripts and creates Jira tickets from extracted action items using Azure OpenAI.

## Features

- 📄 **Multi-format Support**: Processes .txt, .md, .json, .xml, .docx, and .pdf files
- 🤖 **AI-Powered Extraction**: Uses Azure OpenAI to intelligently extract action items
- 🔍 **AI/ML Validation**: Cross-validation, hallucination detection, and consistency management
- 🎯 **Context-Aware Processing**: Adapts extraction based on meeting type and language
- 🎫 **Jira Integration**: Automatically creates and updates Jira tickets
- 📁 **File Monitoring**: Watches for new files and processes them automatically
- ⚡ **Concurrent Processing**: Handles multiple files simultaneously with configurable limits
- 🔄 **Fallback Processing**: Works without AI configuration using rule-based extraction
- 📦 **Auto-Archiving**: Archives processed files with timestamps and status
- 🎛️ **Runtime Configuration**: All AI/ML features can be toggled via environment variables

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- Azure OpenAI account (optional, for AI-powered extraction)
- Jira account with API access (optional, for actual ticket creation)

### Installation

1. Clone the repository
2. Navigate to the project directory
3. Restore dependencies:
   ```powershell
   dotnet restore
   ```

### Configuration

The application is fully configured through the `.env` file in the project root. All settings are optional - the application will work in simulation/fallback mode if API credentials are not provided.

1. Copy `.env.example` to `.env` and update with your settings:

```powershell
Copy-Item .env.example .env
```

2. Edit the `.env` file with your actual credentials:

```env
# Azure OpenAI Configuration (Optional - enables AI extraction)
AOAI_ENDPOINT=https://your-resource-name.openai.azure.com/
AOAI_APIKEY=your-api-key-here
CHATCOMPLETION_DEPLOYMENTNAME=gpt-35-turbo

# Jira Configuration (Optional - enables actual ticket creation)
JIRA_URL=https://your-domain.atlassian.net
JIRA_API_TOKEN=your-api-token-here
JIRA_EMAIL=your-email@company.com
JIRA_PROJECT_KEY=TASK

# File Processing Configuration
INCOMING_DIRECTORY=Data\Incoming
ARCHIVE_DIRECTORY=Data\Archive
PROCESSING_DIRECTORY=Data\Processing
```

### Running the Application

```powershell
dotnet run
```

## Usage

### Processing Files

1. **Start the application** - It will monitor the `data/Incoming` directory
2. **Add transcript files** - Place meeting transcript files in the `data/Incoming` folder
3. **Automatic processing** - Files are automatically:
   - Moved to `data/Processing`
   - Analyzed for action items
   - Used to create/update Jira tickets
   - Archived to `data/Archive` with status and timestamp

### Supported File Formats

- **Text files** (.txt, .md): Plain text transcripts
- **JSON files** (.json): Structured transcript data
- **XML files** (.xml): XML-formatted transcripts
- **Word documents** (.docx): Microsoft Word transcripts
- **PDF files** (.pdf): PDF transcript documents

### Sample Transcript Format

```text
Meeting: Weekly Team Standup - Project Phoenix
Date: 2024-01-15
Participants: Alice Johnson, Bob Smith, Carol Davis

Meeting Transcript:

Alice: We need to fix the authentication bug by Friday.
Bob: I'll investigate the password reset issue this week.
Carol: Let's schedule a UI review meeting with the product team.

Action Items:
- Bob: Fix password reset bug (Due: Friday)
- Carol: Schedule UI review meeting (Due: This week)
```

## Application Flow

```
File Detection → Transcript Processing → Action Item Extraction → Jira Ticket Creation → File Archiving
```

### Detailed Process

1. **File Monitoring**: FileWatcherService monitors the `data/Incoming` directory
2. **Content Extraction**: TranscriptProcessorService reads and parses file content
3. **Metadata Extraction**: Extracts meeting title, date, participants, project keys
4. **AI Analysis**: AzureOpenAIService analyzes content to extract action items
5. **Ticket Operations**: JiraTicketService creates or updates Jira tickets
6. **Archiving**: Files are moved to archive with processing status

## Services Architecture

### Core Services

- **FileWatcherService**: Monitors incoming directory for new files
- **TranscriptProcessorService**: Parses transcripts and extracts action items
- **AzureOpenAIService**: Processes content using Azure OpenAI for intelligent extraction
- **JiraTicketService**: Creates and updates Jira tickets from action items

### AI/ML Validation Services

- **ActionItemValidator**: Cross-validates AI extraction against rule-based results
- **HallucinationDetector**: Analyzes extracted items for AI hallucinations
- **ConsistencyManager**: Ensures consistent extraction across meeting types and languages

### Models

- **MeetingTranscript**: Meeting metadata and content
- **ActionItem**: Extracted tasks/issues from transcript
- **TicketCreationResult**: Result of Jira operations

## Configuration Options

### Azure OpenAI Settings

| Variable                        | Description                                   | Required | Fallback Behavior     |
| ------------------------------- | --------------------------------------------- | -------- | --------------------- |
| `AOAI_ENDPOINT`                 | Your Azure OpenAI resource endpoint           | No\*     | Rule-based extraction |
| `AOAI_APIKEY`                   | Your API key                                  | No\*     | Pattern matching      |
| `CHATCOMPLETION_DEPLOYMENTNAME` | Model deployment name (default: gpt-35-turbo) | No       | N/A                   |

\*Without Azure OpenAI, the app uses rule-based keyword detection and pattern matching for action item extraction

### Jira Settings

| Variable           | Description                         | Required | Fallback Behavior   |
| ------------------ | ----------------------------------- | -------- | ------------------- |
| `JIRA_URL`         | Your Jira instance URL              | No\*     | Simulation mode     |
| `JIRA_API_TOKEN`   | Your Jira API token                 | No\*     | Console-only output |
| `JIRA_EMAIL`       | Your Jira account email             | No\*     | No actual tickets   |
| `JIRA_PROJECT_KEY` | Default project key (default: TASK) | No       | N/A                 |

\*Without Jira configuration, the app shows what tickets would be created but doesn't create actual tickets

### File Processing Settings

| Variable               | Description                                 | Required | Default Value   |
| ---------------------- | ------------------------------------------- | -------- | --------------- |
| `INCOMING_DIRECTORY`   | Directory to watch for new transcript files | No       | Data\Incoming   |
| `ARCHIVE_DIRECTORY`    | Directory for processed files               | No       | Data\Archive    |
| `PROCESSING_DIRECTORY` | Temporary directory during processing       | No       | Data\Processing |

### AI/ML Validation and Reliability Settings

| Variable                       | Description                                           | Required | Default Value |
| ------------------------------ | ----------------------------------------------------- | -------- | ------------- |
| `ENABLE_VALIDATION`            | Enable cross-validation between AI and rule-based extraction | No   | true          |
| `ENABLE_HALLUCINATION_DETECTION` | Enable detection of AI hallucinations in extracted items | No   | true          |
| `ENABLE_CONSISTENCY_MANAGEMENT`  | Enable context-aware extraction based on meeting type | No   | true          |
| `VALIDATION_CONFIDENCE_THRESHOLD` | Minimum confidence score for action items (0.0-1.0) | No   | 0.5           |

### Concurrency Settings

| Variable             | Description                                       | Required | Default Value |
| -------------------- | ------------------------------------------------- | -------- | ------------- |
| `MAX_CONCURRENT_FILES` | Maximum number of files to process simultaneously | No     | 3             |

## Commands

While the application is running, you can use these commands:

- `status` or `s` - Show current system status
- `metrics` or `m` - Show AI validation metrics
- `help` or `h` - Show help information
- `quit` or `q` - Exit the application
- `Ctrl+C` - Graceful shutdown

## AI/ML Validation and Reliability

The application includes enterprise-grade AI/ML validation features that can be toggled on/off via environment variables:

### Cross-Validation (`ENABLE_VALIDATION`)
- Compares AI-extracted action items with rule-based extraction
- Detects potential false positives and false negatives
- Provides confidence scoring for extracted items
- Tracks validation metrics over time

### Hallucination Detection (`ENABLE_HALLUCINATION_DETECTION`)
- Analyzes extracted action items for AI hallucinations
- Validates context snippets exist in the original transcript
- Checks assignee names against meeting participants
- Filters out items with low confidence scores

### Consistency Management (`ENABLE_CONSISTENCY_MANAGEMENT`)
- Automatically detects meeting type (standup, sprint, architecture, etc.)
- Adapts extraction prompts based on meeting context
- Supports multi-language transcript processing
- Optimizes AI parameters for different meeting types

### Runtime Toggles
Set any of these to `false` in your `.env` file to disable temporarily:
```env
ENABLE_VALIDATION=false              # Disable cross-validation
ENABLE_HALLUCINATION_DETECTION=false # Disable hallucination detection
ENABLE_CONSISTENCY_MANAGEMENT=false  # Disable context-aware processing
VALIDATION_CONFIDENCE_THRESHOLD=0.3  # Lower threshold for more permissive filtering
```

Use the `metrics` command while the application is running to monitor validation statistics in real-time.

## Troubleshooting

### Common Issues

1. **Files not processing**: Check file permissions and formats
2. **AI extraction failing**: Verify Azure OpenAI configuration
3. **Jira tickets not created**: Check Jira API credentials and permissions
4. **Performance issues**: Monitor Azure OpenAI rate limits

### Logs

The application provides detailed console output showing:

- File detection and processing status
- AI analysis results
- Jira ticket creation/update results
- Error messages and warnings

## Example Output

```
📄 Processing file: meeting_transcript.txt
🤖 Analyzing transcript with AI to extract action items...
✅ Azure OpenAI analysis completed
🎯 Found 3 action items
🎫 Processing 3 action items for Jira tickets
✅ Created Jira ticket: TASK-1234
✅ Created Jira ticket: TASK-1235
✅ Created Jira ticket: TASK-1236
📊 Processing Results:
   📋 Action Items Found: 3
   🆕 Tickets Created: 3
   📝 Tickets Updated: 0
   ⏱️  Processing Time: 2.3s
   ✅ Success: Yes
📦 Archived: 20240115_143022_success_meeting_transcript.txt
```

## Development

### Building

```powershell
dotnet build
```

### Running Tests

```powershell
dotnet test
```

### Publishing

```powershell
dotnet publish -c Release
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Operation Modes

The application can run in different modes depending on your configuration:

### 🤖 Full AI Mode (Recommended)

- **Requirements**: Azure OpenAI + Jira API credentials
- **Features**:
  - Intelligent action item extraction with context understanding
  - AI-powered ticket formatting with clean titles and descriptions
  - Actual Jira ticket creation and updates
  - Best accuracy and formatting

### 🔧 Hybrid Mode

- **Requirements**: Either Azure OpenAI OR Jira API credentials
- **Features**:
  - With Azure OpenAI only: Smart extraction + simulated tickets
  - With Jira only: Rule-based extraction + actual tickets
  - Partial automation with some manual oversight needed

### ⚙️ Offline Mode

- **Requirements**: No API credentials needed
- **Features**:
  - Rule-based action item extraction using keywords and patterns
  - Simulated ticket creation (console output only)
  - Fully functional for testing and development
  - No external dependencies or costs

### Example Output by Mode

**Full AI Mode:**

```
🤖 Calling Azure OpenAI for transcript analysis...
🎫 Formatting Jira ticket for: Update authentication system
✅ Created Jira ticket: TASK-1234
```

**Offline Mode:**

```
⚙️ Using rule-based fallback processing...
🎫 Would create new ticket: SIM-001
   📋 Title: Update authentication system
   🔹 Type: Task
   ⭐ Priority: Medium
```
