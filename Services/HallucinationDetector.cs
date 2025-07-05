using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Service for detecting potential hallucinations in AI-extracted action items
/// </summary>
public class HallucinationDetector : IHallucinationDetector
{
    private readonly ILogger? _logger;

    public HallucinationDetector(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Detect potential hallucinations in AI-extracted action items
    /// </summary>
    public HallucinationAnalysis AnalyzeForHallucinations(
        List<ActionItem> actionItems,
        MeetingTranscript transcript
    )
    {
        var analysis = new HallucinationAnalysis
        {
            TotalActionItems = actionItems.Count,
            TranscriptTitle = transcript.Title
        };

        foreach (var item in actionItems)
        {
            var itemAnalysis = AnalyzeActionItem(item, transcript);
            analysis.ItemAnalyses.Add(itemAnalysis);

            if (itemAnalysis.IsLikelyHallucination)
            {
                analysis.LikelyHallucinations.Add(item);
                analysis.HallucinationReasons.AddRange(itemAnalysis.HallucinationIndicators);
            }
        }

        analysis.HallucinationRate =
            analysis.TotalActionItems > 0
                ? (double)analysis.LikelyHallucinations.Count / analysis.TotalActionItems
                : 0.0;

        return analysis;
    }

    /// <summary>
    /// Analyze a single action item for hallucination indicators
    /// </summary>
    private ActionItemAnalysis AnalyzeActionItem(ActionItem item, MeetingTranscript transcript)
    {
        var analysis = new ActionItemAnalysis
        {
            ActionItem = item,
            ConfidenceScore = 1.0 // Start with full confidence
        };

        // 1. Context Verification
        if (!string.IsNullOrEmpty(item.Context))
        {
            var contextExists = transcript.Content.Contains(
                item.Context.Substring(0, Math.Min(30, item.Context.Length)),
                StringComparison.OrdinalIgnoreCase
            );

            if (!contextExists)
            {
                analysis.HallucinationIndicators.Add("Context snippet not found in transcript");
                analysis.ConfidenceScore -= 0.4;
            }
        }
        else
        {
            analysis.HallucinationIndicators.Add("No context provided by AI");
            analysis.ConfidenceScore -= 0.2;
        }

        // 2. Assignee Validation
        if (!string.IsNullOrEmpty(item.AssignedTo))
        {
            var assigneeValid = ValidateAssignee(item.AssignedTo, transcript);
            if (!assigneeValid)
            {
                analysis.HallucinationIndicators.Add(
                    $"Assigned person '{item.AssignedTo}' not found in transcript or participants"
                );
                analysis.ConfidenceScore -= 0.3;
            }
        }

        // 3. Keyword Verification
        var keywordScore = VerifyKeywordsInTranscript(item, transcript);
        analysis.ConfidenceScore *= keywordScore;

        if (keywordScore < 0.3)
        {
            analysis.HallucinationIndicators.Add(
                "Key terms from action item not found in transcript"
            );
        }

        // 4. Structural Anomalies
        DetectStructuralAnomalies(item, analysis);

        // 5. Temporal Consistency
        ValidateTemporalConsistency(item, transcript, analysis);

        // 6. Topic Coherence
        var topicScore = ValidateTopicCoherence(item, transcript);
        analysis.ConfidenceScore *= topicScore;

        if (topicScore < 0.4)
        {
            analysis.HallucinationIndicators.Add(
                "Action item topic doesn't match meeting discussion"
            );
        }

        analysis.IsLikelyHallucination =
            analysis.ConfidenceScore < 0.5 || analysis.HallucinationIndicators.Count >= 3;

        return analysis;
    }

    /// <summary>
    /// Validate that assigned person was mentioned in transcript
    /// </summary>
    private bool ValidateAssignee(string assignee, MeetingTranscript transcript)
    {
        // Check participants list
        var inParticipants = transcript.Participants.Any(
            p =>
                p.Contains(assignee, StringComparison.OrdinalIgnoreCase)
                || assignee.Contains(p, StringComparison.OrdinalIgnoreCase)
        );

        // Check transcript content
        var inContent = transcript.Content.Contains(assignee, StringComparison.OrdinalIgnoreCase);

        // Common name variations
        var nameVariations = GenerateNameVariations(assignee);
        var variationFound = nameVariations.Any(
            variation =>
                transcript.Content.Contains(variation, StringComparison.OrdinalIgnoreCase)
                || transcript.Participants.Any(
                    p => p.Contains(variation, StringComparison.OrdinalIgnoreCase)
                )
        );

        return inParticipants || inContent || variationFound;
    }

    /// <summary>
    /// Generate common name variations (John -> John Smith, J. Smith, etc.)
    /// </summary>
    private List<string> GenerateNameVariations(string name)
    {
        var variations = new List<string> { name };

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            variations.Add(parts[0]); // First name only
            variations.Add(parts.Last()); // Last name only
            variations.Add($"{parts[0][0]}. {parts.Last()}"); // J. Smith
        }

        return variations;
    }

    /// <summary>
    /// Verify that action item keywords exist in transcript
    /// </summary>
    private double VerifyKeywordsInTranscript(ActionItem item, MeetingTranscript transcript)
    {
        var allText = $"{item.Title} {item.Description}".ToLowerInvariant();
        var words = allText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Distinct()
            .ToList();

        if (!words.Any())
            return 0.0;

        var stopWords = new[]
        {
            "the",
            "and",
            "for",
            "with",
            "this",
            "that",
            "item",
            "task",
            "jira",
            "ticket"
        };
        var meaningfulWords = words.Where(w => !stopWords.Contains(w)).ToList();

        if (!meaningfulWords.Any())
            return 0.5; // Neutral score if no meaningful words

        var foundWords = meaningfulWords.Count(
            word => transcript.Content.Contains(word, StringComparison.OrdinalIgnoreCase)
        );

        return (double)foundWords / meaningfulWords.Count;
    }

    /// <summary>
    /// Detect structural anomalies in action items
    /// </summary>
    private void DetectStructuralAnomalies(ActionItem item, ActionItemAnalysis analysis)
    {
        // Overly generic titles
        var genericTitles = new[]
        {
            "follow up",
            "check status",
            "review items",
            "discuss further",
            "action item"
        };
        if (
            genericTitles.Any(
                generic => item.Title.Contains(generic, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            analysis.HallucinationIndicators.Add("Action item title is too generic");
            analysis.ConfidenceScore -= 0.2;
        }

        // Suspicious technical terms not in meeting context
        var technicalTerms = new[]
        {
            "kubernetes",
            "docker",
            "microservices",
            "api",
            "database",
            "deployment"
        };
        var itemText = $"{item.Title} {item.Description}".ToLowerInvariant();
        var hasTechnicalTerms = technicalTerms.Any(term => itemText.Contains(term));

        if (hasTechnicalTerms)
        {
            // Check if meeting actually discussed these technical topics
            var transcriptHasTechnical = technicalTerms.Any(
                term => item.Context.Contains(term, StringComparison.OrdinalIgnoreCase)
            );

            if (!transcriptHasTechnical)
            {
                analysis.HallucinationIndicators.Add(
                    "Contains technical terms not discussed in meeting"
                );
                analysis.ConfidenceScore -= 0.3;
            }
        }

        // Unrealistic complexity
        if (item.Title.Length > 150 || item.Description.Length > 500)
        {
            analysis.HallucinationIndicators.Add("Action item is unusually complex/verbose");
            analysis.ConfidenceScore -= 0.1;
        }
    }

    /// <summary>
    /// Validate temporal consistency (due dates, timeline mentions)
    /// </summary>
    private void ValidateTemporalConsistency(
        ActionItem item,
        MeetingTranscript transcript,
        ActionItemAnalysis analysis
    )
    {
        if (item.DueDate.HasValue)
        {
            var dueDate = item.DueDate.Value;
            var meetingDate = transcript.MeetingDate;

            // Due date should be after meeting date
            if (dueDate < meetingDate)
            {
                analysis.HallucinationIndicators.Add("Due date is before meeting date");
                analysis.ConfidenceScore -= 0.3;
            }

            // Due date shouldn't be too far in future (>1 year) without explicit mention
            if (dueDate > meetingDate.AddYears(1))
            {
                var hasLongTermMention =
                    transcript.Content.Contains("next year", StringComparison.OrdinalIgnoreCase)
                    || transcript.Content.Contains("2026", StringComparison.OrdinalIgnoreCase)
                    || transcript.Content.Contains("long term", StringComparison.OrdinalIgnoreCase);

                if (!hasLongTermMention)
                {
                    analysis.HallucinationIndicators.Add("Unrealistic due date without context");
                    analysis.ConfidenceScore -= 0.2;
                }
            }
        }
    }

    /// <summary>
    /// Validate that action item topic aligns with meeting discussion
    /// </summary>
    private double ValidateTopicCoherence(ActionItem item, MeetingTranscript transcript)
    {
        // Extract topic keywords from transcript
        var transcriptTopics = ExtractTopicKeywords(transcript.Content);
        var itemTopics = ExtractTopicKeywords($"{item.Title} {item.Description}");

        if (!transcriptTopics.Any() || !itemTopics.Any())
            return 0.7; // Neutral score

        var overlap = transcriptTopics
            .Intersect(itemTopics, StringComparer.OrdinalIgnoreCase)
            .Count();
        var union = transcriptTopics.Union(itemTopics, StringComparer.OrdinalIgnoreCase).Count();

        return union > 0 ? (double)overlap / union : 0.0;
    }

    /// <summary>
    /// Extract topic-related keywords from text
    /// </summary>
    private List<string> ExtractTopicKeywords(string text)
    {
        var topicWords = new[]
        {
            // Technology
            "system",
            "application",
            "software",
            "code",
            "database",
            "api",
            "server",
            "client",
            "frontend",
            "backend",
            "architecture",
            "security",
            "performance",
            "testing",
            // Business
            "project",
            "budget",
            "timeline",
            "deadline",
            "client",
            "customer",
            "user",
            "requirement",
            "feature",
            "product",
            "market",
            "sales",
            "revenue",
            "cost",
            // Process
            "process",
            "workflow",
            "procedure",
            "policy",
            "documentation",
            "training",
            "review",
            "approval",
            "deployment",
            "release",
            "migration"
        };

        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => topicWords.Contains(word.ToLowerInvariant()))
            .Select(word => word.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Analyzes action items for hallucination indicators (interface implementation)
    /// </summary>
    public HallucinationAnalysis AnalyzeActionItems(
        List<ActionItem> actionItems,
        MeetingTranscript transcript
    )
    {
        return AnalyzeForHallucinations(actionItems, transcript);
    }

    /// <summary>
    /// Filters action items to only include high-confidence items
    /// </summary>
    public List<ActionItem> FilterHighConfidenceItems(
        List<ActionItem> actionItems,
        MeetingTranscript transcript,
        double minConfidence = 0.7
    )
    {
        var analysis = AnalyzeForHallucinations(actionItems, transcript);

        return analysis.ItemAnalyses
            .Where(item => item.ConfidenceScore >= minConfidence && !item.IsLikelyHallucination)
            .Select(item => item.ActionItem)
            .ToList();
    }
}
