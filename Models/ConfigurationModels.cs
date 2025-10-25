using System.ComponentModel;

namespace MeetingTranscriptProcessor.Models;

/// <summary>
/// Configuration settings for Azure OpenAI service
/// </summary>
public class AzureOpenAISettings
{
    /// <summary>
    /// Azure OpenAI endpoint URL
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Azure OpenAI API key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Azure OpenAI deployment name
    /// </summary>
    [DefaultValue("gpt-4")]
    public string DeploymentName { get; set; } = "gpt-4";

    /// <summary>
    /// Maximum tokens for completion
    /// </summary>
    [DefaultValue(4000)]
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Temperature for AI responses (0.0-1.0)
    /// </summary>
    [DefaultValue(0.1)]
    public double Temperature { get; set; } = 0.1;

    /// <summary>
    /// Top P for AI responses (0.0-1.0)
    /// </summary>
    [DefaultValue(0.95)]
    public double TopP { get; set; } = 0.95;

    /// <summary>
    /// API version for Azure OpenAI
    /// </summary>
    [DefaultValue("2024-02-15-preview")]
    public string ApiVersion { get; set; } = "2024-02-15-preview";

    /// <summary>
    /// System prompt for AI assistant
    /// </summary>
    [DefaultValue("You are an expert assistant that analyzes meeting transcripts and extracts actionable items. You respond in valid JSON format.")]
    public string SystemPrompt { get; set; } = "You are an expert assistant that analyzes meeting transcripts and extracts actionable items. You respond in valid JSON format.";

    /// <summary>
    /// Custom prompt to fine-tune AI behavior based on user feedback
    /// </summary>
    public string? CustomPrompt { get; set; }
}

/// <summary>
/// Configuration settings for action item extraction
/// </summary>
public class ExtractionSettings
{
    /// <summary>
    /// Keywords that indicate action items in transcripts
    /// </summary>
    public List<string> ActionKeywords { get; set; } = new()
    {
        "implement", "create", "fix", "review", "update", "add", "remove",
        "investigate", "analyze", "setup", "configure", "test", "develop",
        "build", "deploy", "monitor", "optimize", "refactor", "document"
    };

    /// <summary>
    /// Patterns for identifying action items in text
    /// </summary>
    public List<string> ActionPatterns { get; set; } = new()
    {
        @"action\s*item:",
        @"todo:",
        @"follow\s*up:",
        @"\[\s*\]", // Checkbox
        @"•\s*({keywords})",
        @"-\s*({keywords})",
        @"^\s*\d+\.\s*({keywords})",
        @"({keywords})\s+",
        @"(will|should|need\s+to|must)\s+({keywords})",
        @"create\s+new\s+jira\s+ticket:",
        @"create\s+jira\s+ticket:",
        @"new\s+jira\s+ticket:",
        @"jira\s+ticket:",
        @"assigned\s+to:",
        @"due\s+date:",
        @"priority:"
    };

    /// <summary>
    /// Prefixes to remove when cleaning action item titles
    /// </summary>
    public List<string> TitlePrefixes { get; set; } = new()
    {
        @"^action\s*item:\s*",
        @"^todo:\s*",
        @"^follow\s*up:\s*",
        @"^\[\s*\]\s*",
        @"^[•\-]\s*",
        @"^\d+\.\s*",
        @"^create\s+new\s+jira\s+ticket:\s*",
        @"^create\s+jira\s+ticket:\s*",
        @"^new\s+jira\s+ticket:\s*",
        @"^jira\s+ticket:\s*",
        @"^assigned\s+to:\s*[^-]+[-:]\s*",
        @"^priority:\s*[^-]+[-:]\s*",
        @"^due\s+date:\s*[^-]+[-:]\s*"
    };

    /// <summary>
    /// Keywords that indicate bug-type issues
    /// </summary>
    public List<string> BugKeywords { get; set; } = new()
    {
        "bug", "fix", "error", "issue", "problem", "broken", "crash", "failure"
    };

    /// <summary>
    /// Keywords that indicate investigation tasks
    /// </summary>
    public List<string> InvestigationKeywords { get; set; } = new()
    {
        "investigate", "research", "analyze", "explore", "study", "examine"
    };

    /// <summary>
    /// Keywords that indicate documentation tasks
    /// </summary>
    public List<string> DocumentationKeywords { get; set; } = new()
    {
        "document", "write", "spec", "specification", "manual", "guide", "readme"
    };

    /// <summary>
    /// Keywords that indicate review tasks
    /// </summary>
    public List<string> ReviewKeywords { get; set; } = new()
    {
        "review", "check", "validate", "verify", "approve", "audit"
    };

    /// <summary>
    /// Keywords that indicate story/feature tasks
    /// </summary>
    public List<string> StoryKeywords { get; set; } = new()
    {
        "story", "feature", "enhancement", "improvement", "requirement"
    };

    /// <summary>
    /// Maximum length for action item titles
    /// </summary>
    [DefaultValue(100)]
    public int MaxTitleLength { get; set; } = 100;
}

/// <summary>
/// Configuration settings for meeting type detection
/// </summary>
public class MeetingTypeSettings
{
    /// <summary>
    /// Patterns for detecting standup meetings
    /// </summary>
    public List<string> StandupPatterns { get; set; } = new()
    {
        "standup", "daily", "scrum", "yesterday", "today", "blockers", "impediments"
    };

    /// <summary>
    /// Patterns for detecting sprint meetings
    /// </summary>
    public List<string> SprintPatterns { get; set; } = new()
    {
        "sprint", "retrospective", "retro", "planning", "backlog", "velocity", "burndown"
    };

    /// <summary>
    /// Patterns for detecting architecture meetings
    /// </summary>
    public List<string> ArchitecturePatterns { get; set; } = new()
    {
        "architecture", "design", "technical", "system", "infrastructure", "platform"
    };

    /// <summary>
    /// Patterns for detecting project meetings
    /// </summary>
    public List<string> ProjectPatterns { get; set; } = new()
    {
        "project", "milestone", "deadline", "timeline", "roadmap", "status"
    };

    /// <summary>
    /// Patterns for detecting one-on-one meetings
    /// </summary>
    public List<string> OneOnOnePatterns { get; set; } = new()
    {
        "1:1", "one-on-one", "performance", "feedback", "career", "growth"
    };

    /// <summary>
    /// Patterns for detecting all-hands meetings
    /// </summary>
    public List<string> AllHandsPatterns { get; set; } = new()
    {
        "all-hands", "company", "quarterly", "announcement", "update", "townhall"
    };

    /// <summary>
    /// Maximum participant count for one-on-one detection
    /// </summary>
    [DefaultValue(2)]
    public int OneOnOneMaxParticipants { get; set; } = 2;

    /// <summary>
    /// Minimum content length for standup detection
    /// </summary>
    [DefaultValue(1000)]
    public int StandupMaxContentLength { get; set; } = 1000;
}

/// <summary>
/// Configuration settings for language detection
/// </summary>
public class LanguageSettings
{
    /// <summary>
    /// English language patterns
    /// </summary>
    public List<string> EnglishPatterns { get; set; } = new()
    {
        "the", "and", "is", "to", "in", "that", "have", "for", "not", "with", "he", "as", "you", "do", "at"
    };

    /// <summary>
    /// Spanish language patterns
    /// </summary>
    public List<string> SpanishPatterns { get; set; } = new()
    {
        "el", "la", "de", "que", "y", "en", "un", "es", "se", "no", "te", "lo", "le", "da", "su"
    };

    /// <summary>
    /// French language patterns
    /// </summary>
    public List<string> FrenchPatterns { get; set; } = new()
    {
        "le", "de", "et", "être", "à", "il", "avoir", "ne", "je", "son", "que", "se", "qui", "ce", "dans"
    };

    /// <summary>
    /// German language patterns
    /// </summary>
    public List<string> GermanPatterns { get; set; } = new()
    {
        "der", "die", "und", "in", "den", "von", "zu", "das", "mit", "sich", "des", "auf", "für", "ist", "im"
    };

    /// <summary>
    /// Portuguese language patterns
    /// </summary>
    public List<string> PortuguesePatterns { get; set; } = new()
    {
        "de", "a", "o", "que", "e", "do", "da", "em", "um", "para", "é", "com", "não", "uma", "os"
    };
}

/// <summary>
/// Configuration settings for prompt templates
/// </summary>
public class PromptSettings
{
    /// <summary>
    /// Base extraction prompt template
    /// </summary>
    public string BaseExtractionPrompt { get; set; } = @"
Please analyze the following meeting transcript and extract action items. 
For each action item, provide:
1. Title (brief, actionable)
2. Description (detailed)
3. Assigned person (if mentioned)
4. Due date (if mentioned)
5. Priority (High/Medium/Low)
6. Type (Task/Bug/Story/Investigation/Documentation/Review)
7. Context (original text from transcript)

Meeting: {title}
Date: {date}
Participants: {participants}

Transcript:
{content}

Please respond in this JSON format:
{{
  ""actionItems"": [
    {{
      ""title"": ""Action item title"",
      ""description"": ""Detailed description"",
      ""assignedTo"": ""Person name or null"",
      ""dueDate"": ""YYYY-MM-DD or null"",
      ""priority"": ""High/Medium/Low"",
      ""type"": ""Task/Bug/Story/Investigation/Documentation/Review"",
      ""context"": ""Original text from transcript"",
      ""requiresJiraTicket"": true/false
    }}
  ]
}}

Focus on:
- Clear action items with verbs ({actionKeywords})
- Decisions that require follow-up
- Issues or bugs mentioned
- Tasks assigned to specific people
- Deadlines or time-sensitive items";

    /// <summary>
    /// Language-specific base prompts
    /// </summary>
    public Dictionary<string, string> LanguagePrompts { get; set; } = new()
    {
        ["en"] = "You are an expert meeting analyst. Extract actionable items from this English transcript.",
        ["es"] = "Eres un analista experto de reuniones. Extrae elementos accionables de esta transcripción en español.",
        ["fr"] = "Vous êtes un analyste expert en réunions. Extrayez les éléments actionnables de cette transcription française.",
        ["de"] = "Sie sind ein Experte für Meeting-Analysen. Extrahieren Sie umsetzbare Punkte aus diesem deutschen Transkript.",
        ["pt"] = "Você é um analista especialista em reuniões. Extraia itens acionáveis desta transcrição em português."
    };

    /// <summary>
    /// Meeting type specific guidance
    /// </summary>
    public Dictionary<string, string> MeetingTypeGuidance { get; set; } = new()
    {
        ["Standup"] = "Focus on blockers, progress updates, and next steps. Extract specific tasks and impediments.",
        ["Sprint"] = "Look for sprint goals, story assignments, capacity planning, and retrospective action items.",
        ["Architecture"] = "Extract technical decisions, system changes, and implementation tasks.",
        ["Project"] = "Focus on project milestones, deliverables, and timeline-related tasks.",
        ["OneOnOne"] = "Extract personal development goals, feedback items, and career-related actions.",
        ["AllHands"] = "Look for company-wide initiatives, policy changes, and organizational action items."
    };

    /// <summary>
    /// Consistency rules for different languages
    /// </summary>
    public Dictionary<string, string> ConsistencyRules { get; set; } = new()
    {
        ["en"] = "Ensure consistent terminology and maintain professional English throughout the extraction.",
        ["es"] = "Asegúrese de mantener terminología consistente y un español profesional en toda la extracción.",
        ["fr"] = "Assurez-vous d'une terminologie cohérente et maintenez un français professionnel tout au long de l'extraction.",
        ["de"] = "Stellen Sie eine konsistente Terminologie sicher und verwenden Sie durchgehend professionelles Deutsch.",
        ["pt"] = "Garanta terminologia consistente e mantenha português profissional em toda a extração."
    };
}

/// <summary>
/// Master configuration class containing all settings
/// </summary>
public class AppConfiguration
{
    public AzureOpenAISettings AzureOpenAI { get; set; } = new();
    public ExtractionSettings Extraction { get; set; } = new();
    public MeetingTypeSettings MeetingTypes { get; set; } = new();
    public LanguageSettings Languages { get; set; } = new();
    public PromptSettings Prompts { get; set; } = new();
}
