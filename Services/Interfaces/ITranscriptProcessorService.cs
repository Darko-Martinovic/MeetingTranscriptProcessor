using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Interface for transcript processing operations
/// </summary>
public interface ITranscriptProcessorService
{
    Task<MeetingTranscript> ProcessTranscriptAsync(string filePath);
}
