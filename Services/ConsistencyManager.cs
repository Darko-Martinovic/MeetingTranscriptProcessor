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
            ["es"] = new() { "el", "la", "y", "acción", "tarea", "debe", "necesita", "hacer" },
            ["fr"] = new() { "le", "la", "et", "action", "tâche", "doit", "besoin", "faire" },
            ["de"] = new() { "der", "die", "und", "aktion", "aufgabe", "soll", "muss", "braucht" },
            ["pt"] = new() { "o", "a", "e", "ação", "tarefa", "deve", "precisa", "fazer" }
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
            "es"
                => "Eres un asistente experto que analiza transcripciones de reuniones y extrae elementos de acción. Respondes en formato JSON válido.",
            "fr"
                => "Vous êtes un assistant expert qui analyse les transcriptions de réunions et extrait les éléments d'action. Vous répondez en format JSON valide.",
            "de"
                => "Sie sind ein Experte, der Besprechungstranskripte analysiert und Aktionselemente extrahiert. Sie antworten im gültigen JSON-Format.",
            "pt"
                => "Você é um assistente especialista que analisa transcrições de reuniões e extrai itens de ação. Você responde em formato JSON válido.",
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
                    es = "Enfócate en: bloqueos para resolver, tareas para completar hoy, actualizaciones necesarias.",
                    fr = "Concentrez-vous sur: les blocages à résoudre, les tâches à accomplir aujourd'hui, les mises à jour nécessaires.",
                    de = "Konzentrieren Sie sich auf: zu lösende Blockaden, heute zu erledigende Aufgaben, erforderliche Updates.",
                    pt = "Foque em: bloqueios para resolver, tarefas para completar hoje, atualizações necessárias."
                },

            MeetingType.Sprint
                => new
                {
                    en = "Focus on: story assignments, sprint commitments, backlog refinements, impediment removal. Look for specific deliverables and deadlines.",
                    es = "Enfócate en: asignaciones de historias, compromisos del sprint, refinamiento del backlog.",
                    fr = "Concentrez-vous sur: les affectations d'histoires, les engagements de sprint, les raffinements de backlog.",
                    de = "Konzentrieren Sie sich auf: Story-Zuweisungen, Sprint-Verpflichtungen, Backlog-Verfeinerungen.",
                    pt = "Foque em: atribuições de história, compromissos do sprint, refinamentos do backlog."
                },

            MeetingType.Architecture
                => new
                {
                    en = "Focus on: design decisions requiring implementation, technical debt items, architectural changes, documentation updates.",
                    es = "Enfócate en: decisiones de diseño que requieren implementación, elementos de deuda técnica.",
                    fr = "Concentrez-vous sur: les décisions de conception nécessitant une implémentation, les éléments de dette technique.",
                    de = "Konzentrieren Sie sich auf: Designentscheidungen, die eine Implementierung erfordern, technische Schulden.",
                    pt = "Foque em: decisões de design que requerem implementação, itens de dívida técnica."
                },

            MeetingType.Incident
                => new
                {
                    en = "Focus on: immediate fixes, investigation tasks, preventive measures, follow-up actions. Prioritize by urgency.",
                    es = "Enfócate en: correcciones inmediatas, tareas de investigación, medidas preventivas.",
                    fr = "Concentrez-vous sur: les corrections immédiates, les tâches d'enquête, les mesures préventives.",
                    de = "Konzentrieren Sie sich auf: sofortige Korrekturen, Untersuchungsaufgaben, Präventivmaßnahmen.",
                    pt = "Foque em: correções imediatas, tarefas de investigação, medidas preventivas."
                },

            _
                => new
                {
                    en = "Focus on clear, actionable items with specific owners and deadlines. Ignore general discussion unless it leads to concrete actions.",
                    es = "Enfócate en elementos claros y accionables con propietarios específicos y fechas límite.",
                    fr = "Concentrez-vous sur des éléments clairs et exploitables avec des propriétaires spécifiques.",
                    de = "Konzentrieren Sie sich auf klare, umsetzbare Elemente mit spezifischen Eigentümern.",
                    pt = "Foque em itens claros e acionáveis com proprietários específicos e prazos."
                }
        };

        return language switch
        {
            "es" => guidance.es,
            "fr" => guidance.fr,
            "de" => guidance.de,
            "pt" => guidance.pt,
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
            "es"
                => @"Reglas de consistencia:
- Extraer solo elementos que requieren acción específica
- Incluir el contexto original en español
- Mantener nombres propios en su idioma original
- Usar fechas en formato ISO (YYYY-MM-DD)",

            "fr"
                => @"Règles de cohérence:
- Extraire uniquement les éléments nécessitant une action spécifique
- Inclure le contexte original en français
- Conserver les noms propres dans leur langue d'origine
- Utiliser les dates au format ISO (YYYY-MM-DD)",

            "de"
                => @"Konsistenzregeln:
- Nur Elemente extrahieren, die spezifische Maßnahmen erfordern
- Ursprünglichen Kontext auf Deutsch einschließen
- Eigennamen in ihrer ursprünglichen Sprache beibehalten
- Daten im ISO-Format verwenden (YYYY-MM-DD)",

            "pt"
                => @"Regras de consistência:
- Extrair apenas itens que requerem ação específica
- Incluir contexto original em português
- Manter nomes próprios em sua língua original
- Usar datas no formato ISO (YYYY-MM-DD)",

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
            "es"
                => new[]
                {
                    "implementar",
                    "crear",
                    "arreglar",
                    "revisar",
                    "actualizar",
                    "investigar",
                    "analizar",
                    "configurar"
                },
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
            "de"
                => new[]
                {
                    "implementieren",
                    "erstellen",
                    "beheben",
                    "überprüfen",
                    "aktualisieren",
                    "untersuchen",
                    "analysieren",
                    "konfigurieren"
                },
            "pt"
                => new[]
                {
                    "implementar",
                    "criar",
                    "corrigir",
                    "revisar",
                    "atualizar",
                    "investigar",
                    "analisar",
                    "configurar"
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
            RequireAssignee = config.MeetingType == MeetingType.Standup || config.MeetingType == MeetingType.Incident,
            RequireDueDate = config.MeetingType == MeetingType.Incident || config.MeetingType == MeetingType.ProjectPlanning,
            DefaultTimeframe = config.MeetingType == MeetingType.Standup
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
