using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

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
