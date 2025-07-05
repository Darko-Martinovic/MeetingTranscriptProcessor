# Configuration Files

This directory contains JSON configuration files that control various aspects of the Meeting Transcript Processor application. These files provide a way to customize the application behavior without modifying the source code.

## Configuration Files

### `azure-openai.json`

Controls Azure OpenAI service settings:

- **endpoint**: Azure OpenAI endpoint URL
- **apiKey**: Azure OpenAI API key
- **deploymentName**: Model deployment name (default: "gpt-4")
- **maxTokens**: Maximum tokens for completion (default: 4000)
- **temperature**: AI response temperature 0.0-1.0 (default: 0.1)
- **topP**: AI response top P 0.0-1.0 (default: 0.95)
- **apiVersion**: Azure OpenAI API version (default: "2024-02-15-preview")
- **systemPrompt**: Base system prompt for AI assistant

### `extraction.json`

Controls action item extraction logic:

- **actionKeywords**: Words that indicate action items
- **actionPatterns**: Regex patterns for detecting action items
- **titlePrefixes**: Patterns to remove from action item titles
- **bugKeywords**: Keywords for bug-type classification
- **investigationKeywords**: Keywords for investigation-type classification
- **documentationKeywords**: Keywords for documentation-type classification
- **reviewKeywords**: Keywords for review-type classification
- **storyKeywords**: Keywords for story/feature-type classification
- **maxTitleLength**: Maximum length for action item titles

### `meeting-types.json`

Controls meeting type detection:

- **standupPatterns**: Patterns for detecting standup meetings
- **sprintPatterns**: Patterns for detecting sprint meetings
- **architecturePatterns**: Patterns for detecting architecture meetings
- **projectPatterns**: Patterns for detecting project meetings
- **oneOnOnePatterns**: Patterns for detecting one-on-one meetings
- **allHandsPatterns**: Patterns for detecting all-hands meetings
- **oneOnOneMaxParticipants**: Max participants for one-on-one detection
- **standupMaxContentLength**: Max content length for standup detection

### `languages.json`

Controls language detection:

- **englishPatterns**: Common English words for language detection
- **spanishPatterns**: Common Spanish words for language detection
- **frenchPatterns**: Common French words for language detection
- **germanPatterns**: Common German words for language detection
- **portuguesePatterns**: Common Portuguese words for language detection

### `prompts.json`

Controls AI prompts and templates:

- **baseExtractionPrompt**: Main template for action item extraction
- **languagePrompts**: Language-specific system prompts
- **meetingTypeGuidance**: Meeting type-specific extraction guidance
- **consistencyRules**: Language-specific consistency rules

## Configuration Priority

The application loads configuration in this order (later values override earlier ones):

1. **Default values** from the code
2. **Configuration files** from this directory
3. **Environment variables** from `.env` file

## Environment Variable Overrides

You can override any configuration value using environment variables:

### Azure OpenAI Settings

```env
AOAI_ENDPOINT=https://your-resource.openai.azure.com/
AOAI_APIKEY=your-api-key
CHATCOMPLETION_DEPLOYMENTNAME=gpt-35-turbo
AI_MAX_TOKENS=3000
AI_TEMPERATURE=0.2
AI_TOP_P=0.9
AI_API_VERSION=2024-02-15-preview
AI_SYSTEM_PROMPT="Custom system prompt"
```

### Extraction Settings

```env
EXTRACTION_ACTION_KEYWORDS="implement,create,fix,review"
EXTRACTION_BUG_KEYWORDS="bug,fix,error,issue"
EXTRACTION_INVESTIGATION_KEYWORDS="investigate,research"
EXTRACTION_DOCUMENTATION_KEYWORDS="document,write,spec"
EXTRACTION_REVIEW_KEYWORDS="review,check,validate"
EXTRACTION_STORY_KEYWORDS="story,feature,enhancement"
EXTRACTION_MAX_TITLE_LENGTH=120
```

### Meeting Type Settings

```env
MEETING_ONEONONE_MAX_PARTICIPANTS=3
MEETING_STANDUP_MAX_CONTENT_LENGTH=1500
```

## Customization Examples

### Adding New Action Keywords

Edit `extraction.json` to add new keywords:

```json
{
  "actionKeywords": [
    "implement",
    "create",
    "fix",
    "review",
    "update",
    "add",
    "remove",
    "investigate",
    "analyze",
    "setup",
    "configure",
    "test",
    "develop",
    "build",
    "deploy",
    "monitor",
    "optimize",
    "refactor",
    "document",
    "migrate",
    "upgrade",
    "validate",
    "schedule"
  ]
}
```

### Adding New Meeting Type

Edit `meeting-types.json` to add new meeting type patterns:

```json
{
  "incidentPatterns": [
    "incident",
    "outage",
    "postmortem",
    "root cause",
    "emergency"
  ]
}
```

### Customizing AI Parameters

Edit `azure-openai.json` for different AI behavior:

```json
{
  "temperature": 0.3,
  "maxTokens": 6000,
  "systemPrompt": "You are a specialized assistant for technical meeting analysis..."
}
```

### Adding New Language Support

Edit `languages.json` to add language detection:

```json
{
  "italianPatterns": [
    "il",
    "di",
    "che",
    "la",
    "è",
    "e",
    "un",
    "a",
    "per",
    "in",
    "con",
    "non",
    "una",
    "su",
    "le"
  ]
}
```

Then edit `prompts.json` to add language-specific prompts:

```json
{
  "languagePrompts": {
    "it": "Sei un analista esperto di riunioni. Estrai elementi azionabili da questa trascrizione italiana."
  },
  "consistencyRules": {
    "it": "Assicurati terminologia coerente e mantieni italiano professionale durante l'estrazione."
  }
}
```

## Best Practices

1. **Backup configurations** before making changes
2. **Test changes** in a development environment first
3. **Use environment variables** for deployment-specific settings
4. **Keep prompts concise** but comprehensive
5. **Regular expression patterns** should be tested thoroughly
6. **Language patterns** should include the most common words
7. **Meeting type patterns** should be specific enough to avoid false positives

## Validation

The application will validate configuration files on startup and log any issues. Invalid configurations will fall back to default values.

To test your configuration changes:

1. Save the configuration file
2. Restart the application
3. Check the console output for configuration loading messages
4. Use the `status` command to verify loaded settings
5. Process a test file to verify behavior
