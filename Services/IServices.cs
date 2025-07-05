using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Interface for configuration service operations
/// </summary>
public interface IConfigurationService
{
    AppConfiguration GetConfiguration();
    AzureOpenAISettings GetAzureOpenAISettings();
    ExtractionSettings GetExtractionSettings();
    MeetingTypeSettings GetMeetingTypeSettings();
    LanguageSettings GetLanguageSettings();
    PromptSettings GetPromptSettings();
    void ReloadConfiguration();
    Task SaveConfigurationAsync();
    string GetExtractionPrompt(MeetingTranscript transcript, string? meetingType = null, string? language = null);
    string GetSystemPrompt(string? language = null);
}

/// <summary>
/// Interface for Azure OpenAI service operations
/// </summary>
public interface IAzureOpenAIService : IDisposable
{
    Task<string> ProcessTranscriptAsync(string prompt);
    Task<string> FormatJiraTicketAsync(string title, string description, string context, string participants);
    bool IsConfigured();
}

/// <summary>
/// Interface for Jira ticket operations
/// </summary>
public interface IJiraTicketService : IDisposable
{
    Task<TranscriptProcessingResult> ProcessActionItemsAsync(MeetingTranscript transcript);
}

/// <summary>
/// Interface for transcript processing operations
/// </summary>
public interface ITranscriptProcessorService
{
    Task<MeetingTranscript> ProcessTranscriptAsync(string filePath);
}

/// <summary>
/// Interface for file watching operations
/// </summary>
public interface IFileWatcherService : IDisposable
{
    event EventHandler<FileDetectedEventArgs>? FileDetected;
    void Start();
    void Stop();
}

/// <summary>
/// Interface for action item validation operations
/// </summary>
public interface IActionItemValidator
{
    ValidationResult ValidateActionItems(
        List<ActionItem> aiExtracted,
        List<ActionItem> ruleBasedExtracted,
        MeetingTranscript transcript
    );
    static ValidationMetrics GetValidationMetrics() => ActionItemValidator.GetValidationMetrics();
}

/// <summary>
/// Interface for hallucination detection operations
/// </summary>
public interface IHallucinationDetector
{
    HallucinationAnalysis AnalyzeActionItems(List<ActionItem> actionItems, MeetingTranscript transcript);
    List<ActionItem> FilterHighConfidenceItems(List<ActionItem> actionItems, MeetingTranscript transcript, double minConfidence = 0.7);
}

/// <summary>
/// Interface for consistency management operations
/// </summary>
public interface IConsistencyManager
{
    ConsistencyContext CreateConsistencyContext(MeetingTranscript transcript);
    string GenerateContextualPrompt(MeetingTranscript transcript, ConsistencyContext context);
    ExtractionParameters GetOptimalParameters(ConsistencyContext context);
    ExtractionConfiguration CreateExtractionConfiguration(MeetingTranscript transcript);
}
