namespace MeetingTranscriptProcessor.Models;

/// <summary>
/// Meeting type classification
/// </summary>
public enum MeetingType
{
    General,
    Standup,
    Sprint,
    Architecture,
    ProjectPlanning,
    Incident,
    OneOnOne,
    AllHands,
    ClientMeeting
}

/// <summary>
/// Validation result for a single transcript processing
/// </summary>
public class ValidationResult
{
    public DateTime Timestamp { get; set; }
    public string TranscriptTitle { get; set; } = "";
    public int AIExtractedCount { get; set; }
    public int RuleBasedCount { get; set; }
    public double CrossValidationScore { get; set; }
    public double ContextCoherenceScore { get; set; }
    public double KeywordValidationScore { get; set; }
    public double StructuralValidationScore { get; set; }
    public double OverallConfidence { get; set; }
    public List<string> PotentialFalsePositives { get; set; } = new();
    public List<string> PotentialFalseNegatives { get; set; } = new();
}

/// <summary>
/// Aggregated validation metrics
/// </summary>
public class ValidationMetrics
{
    public int TotalValidations { get; set; }
    public double AverageConfidence { get; set; }
    public double AverageCrossValidationScore { get; set; }
    public double AverageContextCoherence { get; set; }
    public int TotalFalsePositives { get; set; }
    public int TotalFalseNegatives { get; set; }
    public double HighConfidenceRate { get; set; }
    public double LowConfidenceRate { get; set; }
}

/// <summary>
/// Analysis result for hallucination detection
/// </summary>
public class HallucinationAnalysis
{
    public string TranscriptTitle { get; set; } = "";
    public int TotalActionItems { get; set; }
    public List<ActionItem> LikelyHallucinations { get; set; } = new();
    public List<string> HallucinationReasons { get; set; } = new();
    public double HallucinationRate { get; set; }
    public List<ActionItemAnalysis> ItemAnalyses { get; set; } = new();
}

/// <summary>
/// Analysis for a single action item
/// </summary>
public class ActionItemAnalysis
{
    public ActionItem ActionItem { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public List<string> HallucinationIndicators { get; set; } = new();
    public bool IsLikelyHallucination { get; set; }
}

/// <summary>
/// Extraction configuration for context-aware processing
/// </summary>
public class ExtractionConfiguration
{
    public MeetingType MeetingType { get; set; }
    public string Language { get; set; } = "en";
    public string SystemPrompt { get; set; } = "";
    public ExtractionParameters ExtractionParameters { get; set; } = new();
    public ValidationRules ValidationRules { get; set; } = new();
}

/// <summary>
/// Parameters for AI extraction
/// </summary>
public class ExtractionParameters
{
    public double Temperature { get; set; } = 0.1;
    public int MaxTokens { get; set; } = 4000;
    public double TopP { get; set; } = 0.95;
    public bool FocusOnImmediate { get; set; } = false;
    public int MinimumActionWords { get; set; } = 1;
    public bool PrioritizeByUrgency { get; set; } = false;
}

/// <summary>
/// Validation rules for extracted action items
/// </summary>
public class ValidationRules
{
    public string Language { get; set; } = "en";
    public MeetingType MeetingType { get; set; }
    public string[] ActionVerbs { get; set; } = Array.Empty<string>();
    public string[] RequiredFields { get; set; } = Array.Empty<string>();
    public int MaxDaysOut { get; set; } = 365;
    public bool RequirePriority { get; set; } = false;
}

/// <summary>
/// Context for consistency management
/// </summary>
public class ConsistencyContext
{
    public MeetingType MeetingType { get; set; }
    public string Language { get; set; } = "en";
    public List<string> ExpectedActionVerbs { get; set; } = new();
    public double ConfidenceThreshold { get; set; } = 0.7;
    public bool RequireAssignee { get; set; } = false;
    public bool RequireDueDate { get; set; } = false;
    public TimeSpan DefaultTimeframe { get; set; } = TimeSpan.FromDays(7);
}
