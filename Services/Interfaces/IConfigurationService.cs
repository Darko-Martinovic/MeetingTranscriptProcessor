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
    Task SaveAzureOpenAISettingsAsync(AzureOpenAISettings settings);
    string GetExtractionPrompt(MeetingTranscript transcript, string? meetingType = null, string? language = null);
    string GetSystemPrompt(string? language = null);
}
