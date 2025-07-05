using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Service for validating and scoring AI-extracted action items
/// </summary>
public class ActionItemValidator : IActionItemValidator
{
    private readonly ILogger? _logger;

    // Validation metrics tracking
    private static readonly List<ValidationResult> _validationHistory = new();

    public ActionItemValidator(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates AI-extracted action items using multiple techniques
    /// </summary>
    public ValidationResult ValidateActionItems(
        List<ActionItem> aiExtracted,
        List<ActionItem> ruleBasedExtracted,
        MeetingTranscript transcript
    )
    {
        var result = new ValidationResult
        {
            Timestamp = DateTime.UtcNow,
            TranscriptTitle = transcript.Title,
            AIExtractedCount = aiExtracted.Count,
            RuleBasedCount = ruleBasedExtracted.Count
        };

        // 1. Cross-validation scoring
        result.CrossValidationScore = CalculateCrossValidationScore(
            aiExtracted,
            ruleBasedExtracted
        );

        // 2. Context coherence validation
        result.ContextCoherenceScore = ValidateContextCoherence(aiExtracted, transcript);

        // 3. Keyword presence validation
        result.KeywordValidationScore = ValidateKeywordPresence(aiExtracted, transcript.Content);

        // 4. Structural validation
        result.StructuralValidationScore = ValidateStructuralIntegrity(aiExtracted);

        // 5. Calculate overall confidence
        result.OverallConfidence = CalculateOverallConfidence(result);

        // 6. Identify potential false positives/negatives
        result.PotentialFalsePositives = IdentifyFalsePositives(aiExtracted, transcript);
        result.PotentialFalseNegatives = IdentifyFalseNegatives(
            aiExtracted,
            ruleBasedExtracted,
            transcript
        );

        // Track for metrics
        _validationHistory.Add(result);

        return result;
    }

    /// <summary>
    /// Calculate cross-validation score between AI and rule-based extraction
    /// </summary>
    private double CalculateCrossValidationScore(List<ActionItem> ai, List<ActionItem> ruleBased)
    {
        if (ai.Count == 0 && ruleBased.Count == 0)
            return 1.0;
        if (ai.Count == 0 || ruleBased.Count == 0)
            return 0.3; // Some penalty but not zero

        var matches = 0;
        foreach (var aiItem in ai)
        {
            foreach (var ruleItem in ruleBased)
            {
                if (CalculateTextSimilarity(aiItem.Title, ruleItem.Title) > 0.7)
                {
                    matches++;
                    break;
                }
            }
        }

        // Jaccard-like similarity
        var union = ai.Count + ruleBased.Count - matches;
        return union > 0 ? (double)matches / union : 0.0;
    }

    /// <summary>
    /// Validate that action items make sense in context
    /// </summary>
    private double ValidateContextCoherence(
        List<ActionItem> actionItems,
        MeetingTranscript transcript
    )
    {
        var score = 0.0;
        var totalItems = actionItems.Count;

        if (totalItems == 0)
            return 1.0;

        foreach (var item in actionItems)
        {
            var contextScore = 0.0;

            // Check if action item keywords exist in transcript
            var keywords = ExtractKeywords(item.Title + " " + item.Description);
            var keywordsFound = keywords.Count(
                kw => transcript.Content.Contains(kw, StringComparison.OrdinalIgnoreCase)
            );

            contextScore += (double)keywordsFound / keywords.Count * 0.4;

            // Check if assigned person was mentioned in participants
            if (!string.IsNullOrEmpty(item.AssignedTo))
            {
                var assigneeInTranscript =
                    transcript.Content.Contains(item.AssignedTo, StringComparison.OrdinalIgnoreCase)
                    || transcript.Participants.Any(
                        p => p.Contains(item.AssignedTo, StringComparison.OrdinalIgnoreCase)
                    );
                contextScore += assigneeInTranscript ? 0.3 : 0.0;
            }
            else
            {
                contextScore += 0.3; // Neutral score for unassigned items
            }

            // Check if context snippet exists in transcript
            if (!string.IsNullOrEmpty(item.Context))
            {
                var contextExists = transcript.Content.Contains(
                    item.Context.Substring(0, Math.Min(50, item.Context.Length)),
                    StringComparison.OrdinalIgnoreCase
                );
                contextScore += contextExists ? 0.3 : 0.0;
            }

            score += Math.Min(1.0, contextScore);
        }

        return score / totalItems;
    }

    /// <summary>
    /// Validate presence of action item keywords in transcript
    /// </summary>
    private double ValidateKeywordPresence(List<ActionItem> actionItems, string transcriptContent)
    {
        if (actionItems.Count == 0)
            return 1.0;

        var actionKeywords = new[]
        {
            "action",
            "todo",
            "task",
            "implement",
            "create",
            "fix",
            "review",
            "update",
            "investigate"
        };
        var hasActionKeywords = actionKeywords.Any(
            kw => transcriptContent.Contains(kw, StringComparison.OrdinalIgnoreCase)
        );

        if (!hasActionKeywords && actionItems.Count > 0)
        {
            return 0.2; // Low score if no action keywords but items extracted
        }

        var validItems = 0;
        foreach (var item in actionItems)
        {
            var itemKeywords = ExtractKeywords(item.Title);
            var keywordsInTranscript = itemKeywords.Count(
                kw => transcriptContent.Contains(kw, StringComparison.OrdinalIgnoreCase)
            );

            if (keywordsInTranscript > 0)
                validItems++;
        }

        return (double)validItems / actionItems.Count;
    }

    /// <summary>
    /// Validate structural integrity of extracted action items
    /// </summary>
    private double ValidateStructuralIntegrity(List<ActionItem> actionItems)
    {
        if (actionItems.Count == 0)
            return 1.0;

        var score = 0.0;

        foreach (var item in actionItems)
        {
            var itemScore = 0.0;

            // Title quality (not empty, reasonable length, contains verbs)
            if (
                !string.IsNullOrWhiteSpace(item.Title)
                && item.Title.Length > 5
                && item.Title.Length < 200
            )
                itemScore += 0.4;

            // Description quality
            if (!string.IsNullOrWhiteSpace(item.Description) && item.Description.Length > 10)
                itemScore += 0.3;

            // Has actionable verbs
            var actionVerbs = new[]
            {
                "implement",
                "create",
                "fix",
                "review",
                "update",
                "add",
                "remove",
                "investigate",
                "analyze",
                "setup",
                "configure",
                "test"
            };
            if (
                actionVerbs.Any(
                    verb =>
                        item.Title.Contains(verb, StringComparison.OrdinalIgnoreCase)
                        || item.Description.Contains(verb, StringComparison.OrdinalIgnoreCase)
                )
            )
                itemScore += 0.3;

            score += itemScore;
        }

        return score / actionItems.Count;
    }

    /// <summary>
    /// Calculate overall confidence score
    /// </summary>
    private double CalculateOverallConfidence(ValidationResult result)
    {
        return (
            result.CrossValidationScore * 0.3
            + result.ContextCoherenceScore * 0.3
            + result.KeywordValidationScore * 0.2
            + result.StructuralValidationScore * 0.2
        );
    }

    /// <summary>
    /// Identify potential false positives
    /// </summary>
    private List<string> IdentifyFalsePositives(
        List<ActionItem> actionItems,
        MeetingTranscript transcript
    )
    {
        var falsePositives = new List<string>();

        foreach (var item in actionItems)
        {
            // Check for common false positive patterns
            if (
                item.Title.Contains("meeting", StringComparison.OrdinalIgnoreCase)
                && item.Title.Contains("scheduled", StringComparison.OrdinalIgnoreCase)
            )
            {
                falsePositives.Add(
                    $"Possible false positive: '{item.Title}' - might be meeting scheduling, not action item"
                );
            }

            if (item.Title.Length < 10)
            {
                falsePositives.Add(
                    $"Possible false positive: '{item.Title}' - too short to be meaningful action item"
                );
            }

            // Check if item is actually a question or statement
            if (
                item.Title.Contains("?")
                || item.Title.StartsWith("What")
                || item.Title.StartsWith("How")
            )
            {
                falsePositives.Add(
                    $"Possible false positive: '{item.Title}' - appears to be a question, not action item"
                );
            }
        }

        return falsePositives;
    }

    /// <summary>
    /// Identify potential false negatives by comparing with rule-based extraction
    /// </summary>
    private List<string> IdentifyFalseNegatives(
        List<ActionItem> aiItems,
        List<ActionItem> ruleItems,
        MeetingTranscript transcript
    )
    {
        var falseNegatives = new List<string>();

        foreach (var ruleItem in ruleItems)
        {
            var found = aiItems.Any(ai => CalculateTextSimilarity(ai.Title, ruleItem.Title) > 0.6);
            if (!found)
            {
                falseNegatives.Add(
                    $"Possible false negative: AI missed '{ruleItem.Title}' that rule-based extraction found"
                );
            }
        }

        return falseNegatives;
    }

    /// <summary>
    /// Calculate text similarity between two strings
    /// </summary>
    private double CalculateTextSimilarity(string text1, string text2)
    {
        if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
            return 0.0;

        var words1 = text1.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = text2.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    /// <summary>
    /// Extract meaningful keywords from text
    /// </summary>
    private List<string> ExtractKeywords(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();

        var stopWords = new[]
        {
            "the",
            "a",
            "an",
            "and",
            "or",
            "but",
            "in",
            "on",
            "at",
            "to",
            "for",
            "of",
            "with",
            "by"
        };

        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 3 && !stopWords.Contains(word.ToLowerInvariant()))
            .Select(word => word.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Get validation metrics for monitoring
    /// </summary>
    public static ValidationMetrics GetValidationMetrics()
    {
        if (_validationHistory.Count == 0)
        {
            return new ValidationMetrics();
        }

        var recent = _validationHistory.TakeLast(100).ToList(); // Last 100 validations

        return new ValidationMetrics
        {
            TotalValidations = _validationHistory.Count,
            AverageConfidence = recent.Average(v => v.OverallConfidence),
            AverageCrossValidationScore = recent.Average(v => v.CrossValidationScore),
            AverageContextCoherence = recent.Average(v => v.ContextCoherenceScore),
            TotalFalsePositives = recent.Sum(v => v.PotentialFalsePositives.Count),
            TotalFalseNegatives = recent.Sum(v => v.PotentialFalseNegatives.Count),
            HighConfidenceRate =
                recent.Count(v => v.OverallConfidence > 0.8) / (double)recent.Count,
            LowConfidenceRate = recent.Count(v => v.OverallConfidence < 0.4) / (double)recent.Count
        };
    }
}
