# Configuration Summary

This document summarizes the environment variables and configuration used by the Meeting Transcript Processor.

## Environment Variables Used

The application reads the following environment variables from the `.env` file:

### Azure OpenAI Configuration (Optional)

- `AOAI_ENDPOINT` - Azure OpenAI endpoint URL
- `AOAI_APIKEY` - Azure OpenAI API key
- `CHATCOMPLETION_DEPLOYMENTNAME` - Model deployment name (defaults to "gpt-35-turbo")

### Jira Configuration (Optional)

- `JIRA_URL` - Jira instance URL
- `JIRA_API_TOKEN` - Jira API token
- `JIRA_EMAIL` - Jira user email
- `JIRA_PROJECT_KEY` - Default project key (defaults to "TASK")

### File Processing Configuration (Optional)

- `INCOMING_DIRECTORY` - Directory to watch for new files (defaults to "Data\Incoming")
- `ARCHIVE_DIRECTORY` - Directory for processed files (defaults to "Data\Archive")
- `PROCESSING_DIRECTORY` - Temporary processing directory (defaults to "Data\Processing")

## Fallback Compatibility

The application also supports legacy environment variable names for backward compatibility:

- `AZURE_OPENAI_ENDPOINT` → `AOAI_ENDPOINT`
- `AZURE_OPENAI_API_KEY` → `AOAI_APIKEY`
- `AZURE_OPENAI_DEPLOYMENT_NAME` → `CHATCOMPLETION_DEPLOYMENTNAME`
- `JIRA_DEFAULT_PROJECT` → `JIRA_PROJECT_KEY`

## Operation Modes

The application automatically determines its operation mode based on available configuration:

1. **Full AI Mode**: Both Azure OpenAI and Jira credentials configured
2. **Hybrid Mode**: Either Azure OpenAI OR Jira credentials configured
3. **Offline Mode**: No external API credentials configured

All modes are fully functional, with fallbacks to rule-based processing and simulation when APIs are unavailable.

## Configuration Validation

The application validates configuration at startup and reports which services are enabled:

- Azure OpenAI connection status
- Jira integration status
- Directory paths validation
- Fallback behaviors activated

This ensures transparency about which features are active and which are running in simulation mode.
