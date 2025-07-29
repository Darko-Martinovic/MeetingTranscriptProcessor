using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Service to ensure consistent action item extraction across meeting types and languages
/// </summary>
public class ConsistencyManager : IConsistencyManager
{
    private readonly ILogger? _logger;

    // Meeting type classification patterns
    private static readonly Dictionary<MeetingType, List<string>> MeetingTypePatterns =
        new()
        {
            [MeetingType.Standup] = new()
            {
                "standup",
                "daily",
                "scrum",
                "sprint check",
                "status update"
            },
            [MeetingType.Sprint] = new()
            {
                "sprint planning",
                "sprint review",
                "sprint retrospective",
                "backlog"
            },
            [MeetingType.Architecture] = new()
            {
                "architecture",
                "design review",
                "technical design",
                "system design"
            },
            [MeetingType.ProjectPlanning] = new()
            {
                "project planning",
                "roadmap",
                "milestone",
                "timeline",
                "deliverable"
            },
            [MeetingType.Incident] = new()
            {
                "incident",
                "postmortem",
                "outage",
                "root cause",
                "incident response"
            },
            [MeetingType.OneOnOne] = new()
            {
                "1:1",
                "one on one",
                "performance review",
                "career discussion"
            },
            [MeetingType.AllHands] = new()
            {
                "all hands",
                "company meeting",
                "town hall",
                "quarterly"
            },
            [MeetingType.ClientMeeting] = new()
            {
                "client",
                "customer",
                "stakeholder",
                "demo",
                "presentation"
            }
        };

    // Language detection patterns
    private static readonly Dictionary<string, List<string>> LanguagePatterns =
        new()
        {
            ["en"] = new() { "the", "and", "action", "item", "task", "should", "will", "need" },
            ["fr"] = new() { "le", "la", "et", "action", "tâche", "doit", "besoin", "faire" },
            ["nl"] = new() { "de", "het", "en", "actie", "taak", "moet", "zal", "nodig" }
        };

    public ConsistencyManager(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create context-aware extraction configuration
    /// </summary>
    public ExtractionConfiguration CreateExtractionConfiguration(MeetingTranscript transcript)
    {
        var config = new ExtractionConfiguration();

        // 1. Detect meeting type
        config.MeetingType = ClassifyMeetingType(transcript);

        // 2. Detect language
        config.Language = DetectLanguage(transcript.Content);

        // 3. Generate appropriate system prompt
        config.SystemPrompt = GenerateSystemPrompt(config.MeetingType, config.Language);

        // 4. Set extraction parameters
        config.ExtractionParameters = GetExtractionParameters(config.MeetingType);

        // 5. Define validation rules
        config.ValidationRules = GetValidationRules(config.MeetingType, config.Language);

        _logger?.LogInformation(
            $"Generated extraction config: Type={config.MeetingType}, Language={config.Language}"
        );

        return config;
    }

    /// <summary>
    /// Classify meeting type based on content and metadata
    /// </summary>
    private MeetingType ClassifyMeetingType(MeetingTranscript transcript)
    {
        var content = $"{transcript.Title} {transcript.Content}".ToLowerInvariant();
        var scores = new Dictionary<MeetingType, int>();

        foreach (var (type, patterns) in MeetingTypePatterns)
        {
            var score = patterns.Count(pattern => content.Contains(pattern));
            scores[type] = score;
        }

        // Additional heuristics
        if (transcript.Participants.Count <= 2)
        {
            scores[MeetingType.OneOnOne] += 2;
        }

        if (
            transcript.Content.Contains("action item", StringComparison.OrdinalIgnoreCase)
            && transcript.Content.Length < 1000
        )
        {
            scores[MeetingType.Standup] += 1;
        }

        return scores.OrderByDescending(s => s.Value).FirstOrDefault().Key;
    }

    /// <summary>
    /// Detect primary language of transcript
    /// </summary>
    private string DetectLanguage(string content)
    {
        var cleanContent = content.ToLowerInvariant();
        var scores = new Dictionary<string, int>();

        foreach (var (language, patterns) in LanguagePatterns)
        {
            var score = patterns.Count(
                pattern =>
                    cleanContent.Contains($" {pattern} ")
                    || cleanContent.StartsWith($"{pattern} ")
                    || cleanContent.EndsWith($" {pattern}")
            );
            scores[language] = score;
        }

        return scores.OrderByDescending(s => s.Value).FirstOrDefault().Key ?? "en";
    }

    /// <summary>
    /// Generate context-aware system prompt
    /// </summary>
    private string GenerateSystemPrompt(MeetingType meetingType, string language)
    {
        var basePrompt = GetBasePromptForLanguage(language);
        var typeSpecificGuidance = GetMeetingTypeGuidance(meetingType, language);
        var consistencyRules = GetConsistencyRules(language);

        return $@"{basePrompt}

{typeSpecificGuidance}

{consistencyRules}

Always respond in valid JSON format with the exact structure specified.";
    }

    /// <summary>
    /// Get base prompt for specific language
    /// </summary>
    private string GetBasePromptForLanguage(string language)
    {
        return language switch
        {
            "fr"
                => "Vous êtes un assistant expert qui analyse les transcriptions de réunions et extrait les éléments d'action. Vous répondez en format JSON valide.",
            "nl"
                => "U bent een expert assistent die vergadertranscripties analyseert en actie-items extraheert. U antwoordt in geldig JSON-formaat.",
            _
                => "You are an expert assistant that analyzes meeting transcripts and extracts actionable items. You respond in valid JSON format."
        };
    }

    /// <summary>
    /// Get meeting type-specific extraction guidance
    /// </summary>
    private string GetMeetingTypeGuidance(MeetingType meetingType, string language)
    {
        var guidance = meetingType switch
        {
            MeetingType.Standup
                => new
                {
                    en = "Focus on: blockers to resolve, tasks to complete today, updates needed. Ignore status reports unless they require action.",
                    fr = "Concentrez-vous sur: les blocages à résoudre, les tâches à accomplir aujourd'hui, les mises à jour nécessaires.",
                    nl = "Focus op: blokkades om op te lossen, taken voor vandaag, benodigde updates. Negeer statusrapporten tenzij actie vereist."
                },

            MeetingType.Sprint
                => new
                {
                    en = "Focus on: story assignments, sprint commitments, backlog refinements, impediment removal. Look for specific deliverables and deadlines.",
                    fr = "Concentrez-vous sur: les affectations d'histoires, les engagements de sprint, les raffinements de backlog.",
                    nl = "Focus op: story toewijzingen, sprint verplichtingen, backlog verfijningen, impediment verwijdering."
                },

            MeetingType.Architecture
                => new
                {
                    en = "Focus on: design decisions requiring implementation, technical debt items, architectural changes, documentation updates.",
                    fr = "Concentrez-vous sur: les décisions de conception nécessitant une implémentation, les éléments de dette technique.",
                    nl = "Focus op: ontwerpbeslissingen die implementatie vereisen, technische schuld items, architecturale wijzigingen."
                },

            MeetingType.Incident
                => new
                {
                    en = "Focus on: immediate fixes, investigation tasks, preventive measures, follow-up actions. Prioritize by urgency.",
                    fr = "Concentrez-vous sur: les corrections immédiates, les tâches d'enquête, les mesures préventives.",
                    nl = "Focus op: onmiddellijke oplossingen, onderzoekstaken, preventieve maatregelen, follow-up acties."
                },

            _
                => new
                {
                    en = "Focus on clear, actionable items with specific owners and deadlines. Ignore general discussion unless it leads to concrete actions.",
                    fr = "Concentrez-vous sur des éléments clairs et exploitables avec des propriétaires spécifiques.",
                    nl = "Focus op duidelijke, uitvoerbare items met specifieke eigenaren en deadlines."
                }
        };

        return language switch
        {
            "fr" => guidance.fr,
            "nl" => guidance.nl,
            _ => guidance.en
        };
    }

    /// <summary>
    /// Get consistency rules for language
    /// </summary>
    private string GetConsistencyRules(string language)
    {
        return language switch
        {
            "fr"
                => @"Règles de cohérence:
- Extraire uniquement les éléments nécessitant une action spécifique
- Inclure le contexte original en français
- Conserver les noms propres dans leur langue d'origine
- Utiliser les dates au format ISO (YYYY-MM-DD)",

            "nl"
                => @"Consistentieregels:
- Alleen items extraheren die specifieke actie vereisen
- Originele context in het Nederlands opnemen
- Eigennamen in hun oorspronkelijke taal behouden
- Datums in ISO-formaat gebruiken (YYYY-MM-DD)",

            _
                => @"Consistency rules:
- Extract only items requiring specific action
- Include original context in English
- Maintain proper nouns in their original language
- Use ISO date format (YYYY-MM-DD)
- Ensure assignee names match meeting participants"
        };
    }

    /// <summary>
    /// Get extraction parameters for meeting type
    /// </summary>
    private ExtractionParameters GetExtractionParameters(MeetingType meetingType)
    {
        return meetingType switch
        {
            MeetingType.Standup
                => new ExtractionParameters
                {
                    Temperature = 0.05, // Very low for consistent daily patterns
                    MaxTokens = 2000,
                    TopP = 0.9,
                    FocusOnImmediate = true,
                    MinimumActionWords = 1
                },

            MeetingType.Architecture
                => new ExtractionParameters
                {
                    Temperature = 0.15, // Slightly higher for technical complexity
                    MaxTokens = 5000,
                    TopP = 0.95,
                    FocusOnImmediate = false,
                    MinimumActionWords = 2
                },

            MeetingType.Incident
                => new ExtractionParameters
                {
                    Temperature = 0.05, // Very low for critical accuracy
                    MaxTokens = 3000,
                    TopP = 0.85,
                    FocusOnImmediate = true,
                    MinimumActionWords = 1,
                    PrioritizeByUrgency = true
                },

            _
                => new ExtractionParameters
                {
                    Temperature = 0.1,
                    MaxTokens = 4000,
                    TopP = 0.95,
                    FocusOnImmediate = false,
                    MinimumActionWords = 1
                }
        };
    }

    /// <summary>
    /// Get validation rules for meeting type and language
    /// </summary>
    private ValidationRules GetValidationRules(MeetingType meetingType, string language)
    {
        var rules = new ValidationRules { Language = language, MeetingType = meetingType };

        // Language-specific action verbs
        rules.ActionVerbs = language switch
        {
            "fr"
                => new[]
                {
                    "implémenter",
                    "créer",
                    "corriger",
                    "réviser",
                    "mettre à jour",
                    "enquêter",
                    "analyser",
                    "configurer"
                },
            "nl"
                => new[]
                {
                    "implementeren",
                    "creëren",
                    "corrigeren",
                    "herzien",
                    "bijwerken",
                    "onderzoeken",
                    "analyseren",
                    "configureren"
                },
            _
                => new[]
                {
                    "implement",
                    "create",
                    "fix",
                    "review",
                    "update",
                    "investigate",
                    "analyze",
                    "configure",
                    "setup",
                    "test"
                }
        };

        // Meeting type-specific validation
        switch (meetingType)
        {
            case MeetingType.Standup:
                rules.RequiredFields = new[] { "title", "assignedTo" };
                rules.MaxDaysOut = 1; // Standup items should be immediate
                break;

            case MeetingType.Incident:
                rules.RequiredFields = new[] { "title", "priority", "assignedTo" };
                rules.MaxDaysOut = 7; // Incident follow-ups should be soon
                rules.RequirePriority = true;
                break;

            case MeetingType.Architecture:
                rules.RequiredFields = new[] { "title", "description", "type" };
                rules.MaxDaysOut = 90; // Architecture items can be longer term
                break;
        }

        return rules;
    }

    /// <summary>
    /// Create consistency context for a meeting transcript (interface implementation)
    /// </summary>
    public ConsistencyContext CreateConsistencyContext(MeetingTranscript transcript)
    {
        var config = CreateExtractionConfiguration(transcript);

        return new ConsistencyContext
        {
            MeetingType = config.MeetingType,
            Language = config.Language,
            ExpectedActionVerbs = config.ValidationRules.ActionVerbs.ToList(),
            ConfidenceThreshold = 0.7,
            RequireAssignee =
                config.MeetingType == MeetingType.Standup
                || config.MeetingType == MeetingType.Incident,
            RequireDueDate =
                config.MeetingType == MeetingType.Incident
                || config.MeetingType == MeetingType.ProjectPlanning,
            DefaultTimeframe =
                config.MeetingType == MeetingType.Standup
                    ? TimeSpan.FromDays(1)
                    : TimeSpan.FromDays(7)
        };
    }

    /// <summary>
    /// Generate contextual prompt for meeting transcript (interface implementation)
    /// </summary>
    public string GenerateContextualPrompt(MeetingTranscript transcript, ConsistencyContext context)
    {
        var config = CreateExtractionConfiguration(transcript);
        return config.SystemPrompt;
    }

    /// <summary>
    /// Get optimal extraction parameters for consistency context (interface implementation)
    /// </summary>
    public ExtractionParameters GetOptimalParameters(ConsistencyContext context)
    {
        return GetExtractionParameters(context.MeetingType);
    }
}
