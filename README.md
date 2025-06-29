# Meeting Transcript Processor

An intelligent application that automatically processes meeting transcripts and creates Jira tickets from extracted action items using Azure OpenAI.

## Features

- 📄 **Multi-format Support**: Processes .txt, .md, .json, .xml, .docx, and .pdf files
- 🤖 **AI-Powered Extraction**: Uses Azure OpenAI to intelligently extract action items
- 🎫 **Jira Integration**: Automatically creates and updates Jira tickets
- 📁 **File Monitoring**: Watches for new files and processes them automatically
- 🔄 **Fallback Processing**: Works without AI configuration using rule-based extraction
- 📦 **Auto-Archiving**: Archives processed files with timestamps and status

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

1. Copy `.env.example` to `.env`
2. Configure your settings:

```env
# Azure OpenAI Configuration (Optional - enables AI extraction)
AZURE_OPENAI_ENDPOINT=https://your-resource-name.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key-here
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4

# Jira Configuration (Optional - enables actual ticket creation)
JIRA_URL=https://your-domain.atlassian.net
JIRA_API_TOKEN=your-api-token-here
JIRA_EMAIL=your-email@company.com
JIRA_DEFAULT_PROJECT=TASK
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

### Models

- **MeetingTranscript**: Meeting metadata and content
- **ActionItem**: Extracted tasks/issues from transcript
- **TicketCreationResult**: Result of Jira operations

## Configuration Options

### Azure OpenAI Settings

| Variable                       | Description                            | Required |
| ------------------------------ | -------------------------------------- | -------- |
| `AZURE_OPENAI_ENDPOINT`        | Your Azure OpenAI resource endpoint    | No\*     |
| `AZURE_OPENAI_API_KEY`         | Your API key                           | No\*     |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | Model deployment name (default: gpt-4) | No       |

\*Without Azure OpenAI configuration, the app uses rule-based extraction

### Jira Settings

| Variable               | Description                         | Required |
| ---------------------- | ----------------------------------- | -------- |
| `JIRA_URL`             | Your Jira instance URL              | No\*     |
| `JIRA_API_TOKEN`       | Your Jira API token                 | No\*     |
| `JIRA_EMAIL`           | Your Jira account email             | No\*     |
| `JIRA_DEFAULT_PROJECT` | Default project key (default: TASK) | No       |

\*Without Jira configuration, the app runs in simulation mode

## Commands

While the application is running, you can use these commands:

- `status` or `s` - Show current system status
- `help` or `h` - Show help information
- `quit` or `q` - Exit the application
- `Ctrl+C` - Graceful shutdown

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
