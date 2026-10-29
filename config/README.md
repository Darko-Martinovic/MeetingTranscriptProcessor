# Configuration Files

This directory contains configuration files for the MeetingTranscriptProcessor application.

## Setup Instructions

1. Copy `azure-openai.json.example` to `azure-openai.json` to change the model settings (deployment, temperature, max tokens, top P, prompts); the Settings page saves them there too
2. The Azure OpenAI endpoint and key are set in `.env` (`AOAI_ENDPOINT`, `AOAI_APIKEY`), never in these files: the key is neither read from nor written to `azure-openai.json`

## Important Security Note

**Never commit files containing real API keys or secrets to Git!**

The following files are automatically ignored by Git:

- `config/azure-openai.json`
- `config/jira.json`

Always use the `.example` files as templates and create your own local configuration files.
