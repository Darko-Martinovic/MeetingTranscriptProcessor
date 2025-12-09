# 📋 Meeting Transcript Processor - Postman Collection

This directory contains Postman collection and environment files for testing the Meeting Transcript Processor Web API with full API versioning support.

## Files

- **`MeetingTranscriptProcessor.postman_collection.json`** - Complete API collection with all endpoints (v1)
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

#### Connection Variables (Required)

- **`protocol`** - API protocol (default: `http`, use `https` for production)
- **`host`** - API host address (default: `localhost`)
- **`port`** - API port number (default: `5100`)
- **`apiVersion`** - API version (default: `v1`)

#### Testing Variables

- **`fileName`** - Sample meeting file name for testing
- **`processingId`** - Sample processing ID for testing

#### Configuration Variables (Optional - for configuration testing)

- **`azureOpenAIEndpoint`** - Your Azure OpenAI endpoint
- **`azureOpenAIApiKey`** - Your Azure OpenAI API key (secret)
- **`azureOpenAIDeployment`** - Your deployment name (e.g., "gpt-4")
- **`jiraUrl`** - Your Jira instance URL
- **`jiraEmail`** - Your Jira account email
- **`jiraApiToken`** - Your Jira API token (secret)
- **`jiraProjectKey`** - Your Jira project key

### 3. Start the Application

Before testing, ensure the Meeting Transcript Processor is running:

```powershell
# In the project root directory
dotnet run --web

# Or use the deployment scripts
.\Deploy\Local\START-EVERYTHING.cmd
```

The API will be available at `http://localhost:5100/api/v1`

## 🔧 API Versioning

This collection supports **API versioning** using URL segment versioning:

- **Current Version:** `v1`
- **URL Pattern:** `{{protocol}}://{{host}}:{{port}}/api/{{apiVersion}}/...`
- **Example:** `http://localhost:5100/api/v1/meetings/folders`

### Changing API Version

To test different API versions, update the `apiVersion` environment variable:

- `v1` - Current stable version
- `v2` - Future version (when available)

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
- **Get Meeting Tickets** - Get Jira tickets for a specific meeting
- **Update Meeting Title** - Update the title of a meeting
- **Move Meeting** - Move a meeting to a different folder
- **Delete Meeting** - Remove a meeting file

### 📤 File Upload

- **Upload Single File** - Upload one transcript file
- **Upload Multiple Files** - Upload multiple transcript files at once

### ⚙️ Configuration

- **Get Configuration** - Retrieve current system settings
- **Get System Status** - Check system health and metrics
- **Update Azure OpenAI Configuration** - Configure AI settings (with temperature, maxTokens, topP)
- **Update Extraction Configuration** - Configure extraction settings
- **Update Jira Configuration** - Configure Jira integration
- **Update Custom Prompts** - Configure custom AI prompts

### 🤖 AI Training

- **Process Training Transcript** - Process a transcript file for AI training with optional temperature override

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
- **4** - Favorites (user-favorited meetings)

## Filter Parameters

When testing Archive folder filtering, you can use these parameters:

| Parameter        | Description             | Example Values                  |
| ---------------- | ----------------------- | ------------------------------- |
| `search`         | Search in title/content | `"sprint"`, `"meeting"`         |
| `status`         | Processing status       | `"success"`, `"error"`          |
| `language`       | Meeting language        | `"en"`, `"es"`, `"fr"`, `"nl"`  |
| `participants`   | Participant names       | `"john,jane"` (comma-separated) |
| `dateFrom`       | Start date              | `"2024-01-01"` (YYYY-MM-DD)     |
| `dateTo`         | End date                | `"2024-12-31"` (YYYY-MM-DD)     |
| `hasJiraTickets` | Has Jira tickets        | `true`, `false`                 |
| `sortBy`         | Sort field              | `"date"`, `"title"`, `"size"`   |
| `sortOrder`      | Sort direction          | `"asc"`, `"desc"`               |

## File Upload Testing

### Supported File Types

- `.txt` - Plain text files
- `.md` - Markdown files
- `.json` - JSON files
- `.xml` - XML files
- `.docx` - Word documents
- `.pdf` - PDF documents
- `.vtt` - WebVTT subtitle files

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
   - `GET /api/v1/configuration/system-status`
2. **Get All Folders** - See available folders and counts
   - `GET /api/v1/meetings/folders`
3. **Upload a File** - Test file upload functionality
   - `POST /api/v1/meetings/upload`
4. **Check Incoming Folder** - Verify file was uploaded
   - `GET /api/v1/meetings/folders/1/meetings`
5. **Wait for Processing** - File should move to Processing then Archive
6. **Get Meeting Details** - View extracted action items
   - `GET /api/v1/meetings/meeting/{fileName}`

### Advanced Testing

1. **Test Filtering** - Use various filter combinations on Archive folder
2. **Test Recent Folder** - Verify last 5 meetings are shown
3. **Test Configuration** - Update Azure OpenAI and Jira settings
4. **Test Multiple Upload** - Upload several files at once
5. **Test AI Training** - Process training transcripts with custom temperature

## Environment Setup Examples

### Local Development (Default)

```
protocol: http
host: localhost
port: 5100
apiVersion: v1
```

**URL:** `http://localhost:5100/api/v1`

### Remote Machine

```
protocol: http
host: 10.74.42.54
port: 5100
apiVersion: v1
```

**URL:** `http://10.74.42.54:5100/api/v1`

### Production (HTTPS)

```
protocol: https
host: api.yourdomain.com
port: 443
apiVersion: v1
```

**URL:** `https://api.yourdomain.com:443/api/v1`

## Troubleshooting

### Common Issues

**"Connection refused" errors:**

- Ensure the backend is running with `dotnet run --web` or deployment scripts
- Check the `protocol`, `host`, and `port` variables in your environment
- Verify the backend is listening on `http://0.0.0.0:5100` for remote access

**"404 Not Found" errors:**

- Verify the `apiVersion` is set to `v1`
- Ensure all URLs include `/api/v1/` in the path
- Check that the API is running with versioning enabled

**"File not found" errors:**

- Update the `fileName` variable with an actual file name from your Archive folder
- Use the "Get Archive Meetings" request to find valid file names

**Upload failures:**

- Ensure file types are supported (.txt, .md, .json, .xml, .docx, .pdf, .vtt)
- Check file size limits
- Verify the backend is not in processing mode

**Temperature not applying in AI Training:**

- Ensure temperature is sent as a form field (not JSON)
- Value should be between 0.0 and 1.0
- Lower values (e.g., 0.1) are more focused, higher values more creative

### Environment Variables Not Working

1. Make sure the correct environment is selected in Postman
2. Check that variable names match exactly (case-sensitive)
3. Use `{{variableName}}` syntax in requests
4. Verify variables are enabled in the environment settings

### CORS Errors

If testing from a web browser or Postman web version:

- Use Postman desktop application instead
- Verify CORS policy includes your IP/domain
- Check `HybridProgram.cs` for allowed origins

## 🔐 Security Notes

- **Secret Variables:** `azureOpenAIApiKey` and `jiraApiToken` are marked as secret
- Never commit actual API keys to version control
- Use environment-specific configurations for different deployment targets
- For production, always use HTTPS (`protocol: https`)

## Support

For issues or questions:

1. Check the main project README.md
2. Verify your `.env` configuration in the backend
3. Check backend logs for error details
4. Ensure all prerequisites are installed (Azure OpenAI, Jira access)
5. Review API versioning documentation

## 📚 Additional Resources

- **Main Documentation:** `../README.md`
- **Deployment Guide:** `../Deploy/README.md`
- **API Versioning:** `../API-VERSIONING.md` (if available)
- **TrainAI Guide:** `../TRAINAI-ENHANCEMENTS.md`
