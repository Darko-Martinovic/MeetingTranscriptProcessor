using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

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
