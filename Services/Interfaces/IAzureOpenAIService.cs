namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Interface for Azure OpenAI service operations
/// </summary>
public interface IAzureOpenAIService : IDisposable
{
    Task<OpenAIResult> ProcessTranscriptAsync(string prompt);
    Task<string> FormatJiraTicketAsync(string title, string description, string context, string participants);
    bool IsConfigured();
}
