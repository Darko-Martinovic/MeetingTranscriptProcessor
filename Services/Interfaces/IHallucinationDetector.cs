using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Interface for hallucination detection operations
/// </summary>
public interface IHallucinationDetector
{
    HallucinationAnalysis AnalyzeActionItems(List<ActionItem> actionItems, MeetingTranscript transcript);
    List<ActionItem> FilterHighConfidenceItems(List<ActionItem> actionItems, MeetingTranscript transcript, double minConfidence = 0.7);
}
