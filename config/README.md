# Configuration Files

This directory contains configuration files for the MeetingTranscriptProcessor application.

## Setup Instructions

1. Copy `azure-openai.json.example` to `azure-openai.json`
2. Fill in your actual Azure OpenAI credentials in `azure-openai.json`
3. The `azure-openai.json` file is **gitignored** to protect your API keys

## Important Security Note

**Never commit files containing real API keys or secrets to Git!**

The following files are automatically ignored by Git:

- `config/azure-openai.json`
- `config/jira.json`

Always use the `.example` files as templates and create your own local configuration files.
