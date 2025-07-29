using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

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
