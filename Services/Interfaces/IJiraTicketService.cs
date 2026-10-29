using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Interface for Jira ticket operations
/// </summary>
public interface IJiraTicketService
{
    Task<TranscriptProcessingResult> ProcessActionItemsAsync(MeetingTranscript transcript);
}
