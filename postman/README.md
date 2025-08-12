# Meeting Transcript Processor - Postman Collection

This directory contains Postman collection and environment files for testing the Meeting Transcript Processor Web API.

## Files

- **`MeetingTranscriptProcessor.postman_collection.json`** - Complete API collection with all endpoints
- **`MeetingTranscriptProcessor.postman_environment.json`** - Environment variables for development
- **`README.md`** - This documentation file

## Setup Instructions

### 1. Import Collection and Environment

1. Open Postman
2. Click **Import** button
3. Select both JSON files from this directory:
   - `MeetingTranscriptProcessor.postman_collection.json`
   - `MeetingTranscriptProcessor.postman_environment.json`
4. Click **Import**

### 2. Configure Environment Variables

1. Select the "Meeting Transcript Processor - Development" environment
2. Update the following variables with your actual values:

#### Required Variables
- **`baseUrl`** - API base URL (default: `http://localhost:5000`)
- **`fileName`** - Sample meeting file name for testing

#### Optional Variables (for configuration testing)
- **`azureOpenAIEndpoint`** - Your Azure OpenAI endpoint
- **`azureOpenAIApiKey`** - Your Azure OpenAI API key
- **`azureOpenAIDeployment`** - Your deployment name (e.g., "gpt-4")
- **`jiraUrl`** - Your Jira instance URL
- **`jiraEmail`** - Your Jira account email
- **`jiraApiToken`** - Your Jira API token
- **`jiraProjectKey`** - Your Jira project key

### 3. Start the Application

Before testing, ensure the Meeting Transcript Processor is running:

```powershell
# In the project root directory
dotnet run --web
```

The API will be available at `http://localhost:5000`

## Collection Structure

### 📊 System Status
- **Get System Status** - Check API health and processing metrics

### 📁 Folder Management
- **Get All Folders** - List all folders with meeting counts
- **Get Archive Meetings** - List meetings in Archive folder
- **Get Archive Meetings with Filters** - Advanced filtering example
- **Get Incoming Meetings** - List meetings in Incoming folder
- **Get Processing Meetings** - List meetings in Processing folder
- **Get Recent Meetings** - List last 5 meetings across all folders

### 📄 Meeting Management
- **Get Meeting Details** - Get full meeting transcript and action items
- **Delete Meeting** - Remove a meeting file

### 📤 File Upload
- **Upload Single File** - Upload one transcript file
- **Upload Multiple Files** - Upload multiple transcript files at once

### ⚙️ Configuration
- **Get Configuration** - Retrieve current system settings
- **Update Azure OpenAI Configuration** - Configure AI settings
- **Update Jira Configuration** - Configure Jira integration

### 🔍 Advanced Filtering Examples
- **Filter by Date Range** - Date-based filtering
- **Filter by Participants** - Participant-based filtering
- **Filter by Jira Ticket Status** - Jira ticket filtering
- **Complex Filter Combination** - Multiple filters combined

## Folder Types

The API uses numeric folder types:
- **0** - Archive (processed meetings)
- **1** - Incoming (new files)
- **2** - Processing (files being analyzed)
- **3** - Recent (last 5 meetings across all folders)

## Filter Parameters

When testing Archive folder filtering, you can use these parameters:

| Parameter | Description | Example Values |
|-----------|-------------|----------------|
| `search` | Search in title/content | `"sprint"`, `"meeting"` |
| `status` | Processing status | `"success"`, `"error"` |
| `language` | Meeting language | `"en"`, `"es"`, `"fr"` |
| `participants` | Participant names | `"john,jane"` (comma-separated) |
| `dateFrom` | Start date | `"2024-01-01"` (YYYY-MM-DD) |
| `dateTo` | End date | `"2024-12-31"` (YYYY-MM-DD) |
| `hasJiraTickets` | Has Jira tickets | `true`, `false` |
| `sortBy` | Sort field | `"date"`, `"title"`, `"size"` |
| `sortOrder` | Sort direction | `"asc"`, `"desc"` |

## File Upload Testing

### Supported File Types
- `.txt` - Plain text files
- `.md` - Markdown files
- `.json` - JSON files
- `.xml` - XML files
- `.docx` - Word documents
- `.pdf` - PDF documents

### Single File Upload
1. Select the "Upload Single File" request
2. Go to the **Body** tab
3. Select a file for the `file` parameter
4. Send the request

### Multiple File Upload
1. Select the "Upload Multiple Files" request
2. Go to the **Body** tab
3. Add multiple `file` parameters with different files
4. Send the request

## Testing Workflow

### Basic API Testing
1. **Start with System Status** - Verify API is running
2. **Get All Folders** - See available folders and counts
3. **Upload a File** - Test file upload functionality
4. **Check Incoming Folder** - Verify file was uploaded
5. **Wait for Processing** - File should move to Processing then Archive
6. **Get Meeting Details** - View extracted action items

### Advanced Testing
1. **Test Filtering** - Use various filter combinations on Archive folder
2. **Test Recent Folder** - Verify last 5 meetings are shown
3. **Test Configuration** - Update Azure OpenAI and Jira settings
4. **Test Multiple Upload** - Upload several files at once

## Troubleshooting

### Common Issues

**"Connection refused" errors:**
- Ensure the backend is running with `dotnet run --web`
- Check the `baseUrl` in your environment

**"File not found" errors:**
- Update the `fileName` variable with an actual file name from your Archive folder
- Use the "Get Archive Meetings" request to find valid file names

**Upload failures:**
- Ensure file types are supported
- Check file size limits
- Verify the backend is not in processing mode

### Environment Variables Not Working
1. Make sure the environment is selected in Postman
2. Check that variable names match exactly (case-sensitive)
3. Use `{{variableName}}` syntax in requests

## Support

For issues or questions:
1. Check the main project README.md
2. Verify your .env configuration
3. Check backend logs for error details
4. Ensure all prerequisites are installed
