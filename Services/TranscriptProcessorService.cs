using MeetingTranscriptProcessor.Models;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Service for parsing and analyzing meeting transcripts to extract action items
/// </summary>
public class TranscriptProcessorService : ITranscriptProcessorService
{
    private readonly IAzureOpenAIService _aiService;
    private readonly IActionItemValidator _validator;
    private readonly IHallucinationDetector _hallucinationDetector;
    private readonly IConsistencyManager _consistencyManager;
    private readonly ILogger? _logger;

    // Validation service control settings - loaded from environment
    private readonly bool _enableValidation;
    private readonly bool _enableHallucinationDetection;
    private readonly bool _enableConsistencyManagement;
    private readonly double _validationConfidenceThreshold;

    public TranscriptProcessorService(
        IAzureOpenAIService aiService,
        IActionItemValidator validator,
        IHallucinationDetector hallucinationDetector,
        IConsistencyManager consistencyManager,
        ILogger? logger = null)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _hallucinationDetector = hallucinationDetector ?? throw new ArgumentNullException(nameof(hallucinationDetector));
        _consistencyManager = consistencyManager ?? throw new ArgumentNullException(nameof(consistencyManager));
        _logger = logger;

        // Load validation settings from environment variables
        _enableValidation = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_VALIDATION"), out var validation) ? validation : true;
        _enableHallucinationDetection = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_HALLUCINATION_DETECTION"), out var hallucination) ? hallucination : true;
        _enableConsistencyManagement = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_CONSISTENCY_MANAGEMENT"), out var consistency) ? consistency : true;
        _validationConfidenceThreshold = double.TryParse(Environment.GetEnvironmentVariable("VALIDATION_CONFIDENCE_THRESHOLD"), out var threshold) ? threshold : 0.5;
    }

    /// <summary>
    /// Processes a transcript file and extracts action items
    /// </summary>
    public async Task<MeetingTranscript> ProcessTranscriptAsync(string filePath)
    {
        try
        {
            Console.WriteLine($"📄 Processing transcript: {Path.GetFileName(filePath)}");

            var transcript = new MeetingTranscript
            {
                FileName = Path.GetFileName(filePath),
                Status = TranscriptStatus.Processing,
                ProcessedAt = DateTime.UtcNow,
                // Read file content
                Content = await ReadFileContentAsync(filePath)
            };

            if (string.IsNullOrWhiteSpace(transcript.Content))
            {
                throw new InvalidOperationException("Transcript content is empty");
            }

            // Extract metadata from content
            ExtractMetadata(transcript);

            // Extract action items using AI
            await ExtractActionItemsAsync(transcript);

            transcript.Status = TranscriptStatus.Processed;

            Console.WriteLine(
                $"✅ Processed transcript: {transcript.ActionItems.Count} action items found"
            );

            return transcript;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error processing transcript: {filePath}");
            Console.WriteLine($"❌ Error processing transcript: {ex.Message}");

            return new MeetingTranscript
            {
                FileName = Path.GetFileName(filePath),
                Status = TranscriptStatus.Error,
                ProcessedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Reads content from various file types
    /// </summary>
    private static async Task<string> ReadFileContentAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".txt" or ".md" => await File.ReadAllTextAsync(filePath),
            ".json" => await ReadJsonContentAsync(filePath),
            ".xml" => await ReadXmlContentAsync(filePath),
            _ => await File.ReadAllTextAsync(filePath) // Fallback to text
        };
    }

    /// <summary>
    /// Reads JSON transcript files (common format from Teams, Zoom, etc.)
    /// </summary>
    private static async Task<string> ReadJsonContentAsync(string filePath)
    {
        try
        {
            var jsonContent = await File.ReadAllTextAsync(filePath);
            var transcriptData = JsonConvert.DeserializeObject<dynamic>(jsonContent);

            // Try common JSON transcript formats
            if (transcriptData?.transcript != null)
            {
                return transcriptData.transcript.ToString();
            }

            if (transcriptData?.content != null)
            {
                return transcriptData.content.ToString();
            }

            if (transcriptData?.text != null)
            {
                return transcriptData.text.ToString();
            }

            // If no standard format, return the JSON as text
            return jsonContent;
        }
        catch
        {
            // If JSON parsing fails, treat as plain text
            return await File.ReadAllTextAsync(filePath);
        }
    }

    /// <summary>
    /// Reads XML transcript files
    /// </summary>
    private static async Task<string> ReadXmlContentAsync(string filePath)
    {
        try
        {
            var xmlContent = await File.ReadAllTextAsync(filePath);

            // Simple extraction of text content from XML
            var textPattern =
                @"<(?:text|content|transcript)[^>]*>(.*?)</(?:text|content|transcript)>";
            var matches = Regex.Matches(
                xmlContent,
                textPattern,
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            if (matches.Count > 0)
            {
                return string.Join("\n", matches.Cast<Match>().Select(m => m.Groups[1].Value));
            }

            // Remove all XML tags and return plain text
            return Regex.Replace(xmlContent, @"<[^>]+>", " ");
        }
        catch
        {
            return await File.ReadAllTextAsync(filePath);
        }
    }

    /// <summary>
    /// Extracts metadata from transcript content
    /// </summary>
    private void ExtractMetadata(MeetingTranscript transcript)
    {
        try
        {
            // Extract title from first line or header
            var lines = transcript.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                transcript.Title =
                    ExtractTitle(lines[0]) ?? Path.GetFileNameWithoutExtension(transcript.FileName);
            }

            // Extract meeting date
            transcript.MeetingDate = ExtractMeetingDate(transcript.Content) ?? DateTime.UtcNow.Date;

            // Extract participants
            transcript.Participants = ExtractParticipants(transcript.Content);

            // Extract project key from filename or content
            transcript.ProjectKey = ExtractProjectKey(
                transcript.FileName + " " + transcript.Content
            );

            _logger?.LogInformation(
                $"Extracted metadata - Title: {transcript.Title}, Participants: {transcript.Participants.Count}"
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error extracting metadata");
            // Continue processing even if metadata extraction fails
        }
    }

    /// <summary>
    /// Extracts action items using AI analysis with optional validation and consistency checking
    /// </summary>
    private async Task ExtractActionItemsAsync(MeetingTranscript transcript)
    {
        List<ActionItem> aiExtracted = new();
        List<ActionItem> ruleBasedExtracted = new();

        try
        {
            Console.WriteLine("🤖 Analyzing transcript with AI to extract action items...");

            // 1. Use consistency manager for optimal extraction configuration (if enabled)
            string promptToUse;
            ConsistencyContext? consistencyContext = null;

            if (_enableConsistencyManagement)
            {
                Console.WriteLine("🎯 Using consistency management for context-aware extraction...");
                consistencyContext = _consistencyManager.CreateConsistencyContext(transcript);
                promptToUse = _consistencyManager.GenerateContextualPrompt(transcript, consistencyContext);
                var optimalParams = _consistencyManager.GetOptimalParameters(consistencyContext);
            }
            else
            {
                Console.WriteLine("⚠️  Consistency management disabled - using standard prompt");
                promptToUse = CreateActionItemExtractionPrompt(transcript);
            }

            // 2. Extract action items with AI using chosen prompt
            var aiResponse = await _aiService.ProcessTranscriptAsync(promptToUse);
            aiExtracted = ParseActionItemsFromAIResponse(aiResponse, transcript);

            // 3. Get rule-based extraction for comparison/validation (if validation enabled)
            if (_enableValidation)
            {
                ruleBasedExtracted = ExtractActionItemsWithRules(transcript);
            }

            // 4. Validate AI extraction using cross-validation (if enabled)
            ValidationResult? validationResult = null;
            if (_enableValidation)
            {
                Console.WriteLine("🔍 Performing validation...");
                validationResult = _validator.ValidateActionItems(aiExtracted, ruleBasedExtracted, transcript);

                Console.WriteLine($"🔍 Validation Results:");
                Console.WriteLine($"   - AI extracted: {aiExtracted.Count} items");
                Console.WriteLine($"   - Rule-based: {ruleBasedExtracted.Count} items");
                Console.WriteLine($"   - Cross-validation score: {validationResult.CrossValidationScore:F2}");
                Console.WriteLine($"   - Overall confidence: {validationResult.OverallConfidence:F2}");
                Console.WriteLine($"   - Potential false positives: {validationResult.PotentialFalsePositives.Count}");
                Console.WriteLine($"   - Potential false negatives: {validationResult.PotentialFalseNegatives.Count}");
            }
            else
            {
                Console.WriteLine("⚠️  Validation disabled - skipping cross-validation");
            }

            // 5. Check for hallucinations (if enabled)
            HallucinationAnalysis? hallucinationAnalysis = null;
            if (_enableHallucinationDetection)
            {
                Console.WriteLine("🧠 Performing hallucination detection...");
                hallucinationAnalysis = _hallucinationDetector.AnalyzeActionItems(aiExtracted, transcript);
                Console.WriteLine($"🧠 Hallucination Analysis:");
                Console.WriteLine($"   - Hallucination rate: {hallucinationAnalysis.HallucinationRate:P}");
                Console.WriteLine($"   - Likely hallucinations: {hallucinationAnalysis.LikelyHallucinations.Count}");
            }
            else
            {
                Console.WriteLine("⚠️  Hallucination detection disabled - skipping hallucination analysis");
            }

            // 6. Filter and apply business rules
            List<ActionItem> finalItems;

            if (_enableHallucinationDetection && hallucinationAnalysis != null)
            {
                // Use confidence threshold from consistency context or default
                var confidenceThreshold = consistencyContext?.ConfidenceThreshold ?? _validationConfidenceThreshold;
                var highConfidenceItems = _hallucinationDetector.FilterHighConfidenceItems(
                    aiExtracted, transcript, confidenceThreshold);
                finalItems = ApplyBusinessRules(highConfidenceItems);
            }
            else
            {
                // Apply business rules directly to AI extracted items
                finalItems = ApplyBusinessRules(aiExtracted);
            }

            transcript.ActionItems = finalItems;

            // 7. Log validation warnings (if validation was performed)
            if (_enableValidation && validationResult != null)
            {
                if (validationResult.PotentialFalsePositives.Any())
                {
                    Console.WriteLine("⚠️  Potential false positives detected:");
                    validationResult.PotentialFalsePositives.ForEach(fp => Console.WriteLine($"   - {fp}"));
                }

                if (validationResult.PotentialFalseNegatives.Any())
                {
                    Console.WriteLine("⚠️  Potential false negatives detected:");
                    validationResult.PotentialFalseNegatives.ForEach(fn => Console.WriteLine($"   - {fn}"));
                }
            }

            if (_enableHallucinationDetection && hallucinationAnalysis != null && hallucinationAnalysis.LikelyHallucinations.Any())
            {
                Console.WriteLine("🚨 Likely hallucinations detected and filtered:");
                hallucinationAnalysis.LikelyHallucinations.ForEach(h =>
                    Console.WriteLine($"   - {h.Title}"));
            }

            // Log final results with service status
            var statusIndicator = "";
            if (!_enableValidation && !_enableHallucinationDetection && !_enableConsistencyManagement)
            {
                statusIndicator = " (⚠️  All validation services disabled)";
            }
            else if (!_enableValidation || !_enableHallucinationDetection || !_enableConsistencyManagement)
            {
                var disabled = new List<string>();
                if (!_enableValidation) disabled.Add("validation");
                if (!_enableHallucinationDetection) disabled.Add("hallucination detection");
                if (!_enableConsistencyManagement) disabled.Add("consistency management");
                statusIndicator = $" (⚠️  {string.Join(", ", disabled)} disabled)";
            }

            Console.WriteLine($"✅ Final result: {transcript.ActionItems.Count} action items{statusIndicator}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error extracting action items with AI");
            Console.WriteLine($"❌ AI extraction failed, falling back to rule-based extraction");

            // Fallback to rule-based extraction
            transcript.ActionItems = ExtractActionItemsWithRules(transcript);
        }
    }

    /// <summary>
    /// Creates a prompt for AI to extract action items
    /// </summary>
    private static string CreateActionItemExtractionPrompt(MeetingTranscript transcript)
    {
        return $@"
Please analyze the following meeting transcript and extract action items. 
For each action item, provide:
1. Title (brief, actionable)
2. Description (detailed)
3. Assigned person (if mentioned)
4. Due date (if mentioned)
5. Priority (High/Medium/Low)
6. Type (Task/Bug/Story/Investigation/Documentation/Review)
7. Context (original text from transcript)

Meeting: {transcript.Title}
Date: {transcript.MeetingDate:yyyy-MM-dd}
Participants: {string.Join(", ", transcript.Participants)}

Transcript:
{transcript.Content}

Please respond in this JSON format:
{{
  ""actionItems"": [
    {{
      ""title"": ""Action item title"",
      ""description"": ""Detailed description"",
      ""assignedTo"": ""Person name or null"",
      ""dueDate"": ""YYYY-MM-DD or null"",
      ""priority"": ""High/Medium/Low"",
      ""type"": ""Task/Bug/Story/Investigation/Documentation/Review"",
      ""context"": ""Original text from transcript"",
      ""requiresJiraTicket"": true/false
    }}
  ]
}}

Focus on:
- Clear action items with verbs (implement, fix, review, create, etc.)
- Decisions that require follow-up
- Issues or bugs mentioned
- Tasks assigned to specific people
- Deadlines or time-sensitive items
";
    }

    /// <summary>
    /// Parses action items from AI response
    /// </summary>
    private static List<ActionItem> ParseActionItemsFromAIResponse(
        string aiResponse,
        MeetingTranscript transcript
    )
    {
        try
        {
            var response = JsonConvert.DeserializeObject<dynamic>(aiResponse);
            var actionItems = new List<ActionItem>();

            if (response?.actionItems != null)
            {
                foreach (var item in response.actionItems)
                {
                    var actionItem = new ActionItem
                    {
                        Title = item.title?.ToString() ?? "Untitled Action Item",
                        Description = item.description?.ToString() ?? "",
                        AssignedTo = item.assignedTo?.ToString(),
                        Context = item.context?.ToString() ?? "",
                        ProjectKey = transcript.ProjectKey,
                        RequiresJiraTicket = item.requiresJiraTicket ?? true
                    };

                    // Parse priority
                    if (
                        Enum.TryParse<ActionItemPriority>(
                            item.priority?.ToString() ?? "Medium",
                            out ActionItemPriority priority
                        )
                    )
                    {
                        actionItem.Priority = priority;
                    }

                    // Parse type
                    if (
                        Enum.TryParse<ActionItemType>(
                            item.type?.ToString() ?? "Task",
                            out ActionItemType type
                        )
                    )
                    {
                        actionItem.Type = type;
                    }

                    // Parse due date
                    if (DateTime.TryParse(item.dueDate?.ToString(), out DateTime dueDate))
                    {
                        actionItem.DueDate = dueDate;
                    }

                    actionItems.Add(actionItem);
                }
            }

            return actionItems;
        }
        catch (Exception)
        {
            // If JSON parsing fails, fall back to text parsing
            return ParseActionItemsFromText(aiResponse, transcript);
        }
    }

    /// <summary>
    /// Fallback method to extract action items using rule-based approach
    /// </summary>
    private static List<ActionItem> ExtractActionItemsWithRules(MeetingTranscript transcript)
    {
        var actionItems = new List<ActionItem>();
        var lines = transcript.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Look for action item patterns
            if (IsActionItemLine(trimmedLine))
            {
                var actionItem = CreateActionItemFromLine(trimmedLine, transcript);
                if (actionItem != null)
                {
                    actionItems.Add(actionItem);
                }
            }
        }

        return actionItems;
    }

    /// <summary>
    /// Checks if a line contains an action item
    /// </summary>
    private static bool IsActionItemLine(string line)
    {
        var actionPatterns = new[]
        {
            @"action\s*item:",
            @"todo:",
            @"follow\s*up:",
            @"\[\s*\]", // Checkbox
            @"•\s*(implement|create|fix|review|update|add|remove|investigate|analyze|setup|configure|test)",
            @"-\s*(implement|create|fix|review|update|add|remove|investigate|analyze|setup|configure|test)",
            @"^\s*\d+\.\s*(implement|create|fix|review|update|add|remove|investigate|analyze|setup|configure|test)",
            @"(implement|create|fix|review|update|add|remove|investigate|analyze|setup|configure|test)\s+",
            @"(will|should|need\s+to|must)\s+(implement|create|fix|review|update|add|remove|investigate|analyze|setup|configure|test)",
            @"create\s+new\s+jira\s+ticket:",
            @"create\s+jira\s+ticket:",
            @"new\s+jira\s+ticket:",
            @"jira\s+ticket:",
            @"assigned\s+to:",
            @"due\s+date:",
            @"priority:"
        };

        return actionPatterns.Any(pattern => Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase));
    }

    /// <summary>
    /// Creates an action item from a line of text
    /// </summary>
    private static ActionItem? CreateActionItemFromLine(string line, MeetingTranscript transcript)
    {
        try
        {
            // Clean up the line and remove common prefixes
            var cleanLine = CleanActionItemTitle(line);
            if (string.IsNullOrEmpty(cleanLine))
                return null;

            return new ActionItem
            {
                Title = cleanLine.Length > 100 ? cleanLine.Substring(0, 100) + "..." : cleanLine,
                Description = cleanLine,
                Context = line,
                ProjectKey = transcript.ProjectKey,
                Priority = ActionItemPriority.Medium,
                Type = DetermineActionItemType(cleanLine)
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Cleans up action item title by removing prefixes and formatting
    /// </summary>
    private static string CleanActionItemTitle(string line)
    {
        // Remove common action item prefixes and markers
        var cleanLine = line.Trim();

        // Remove numbered lists, bullets, checkboxes
        cleanLine = Regex.Replace(cleanLine, @"^[-•\[\]\s\d+\.]+", "").Trim();

        // Remove common action item prefixes
        var prefixPatterns = new[]
        {
            @"^action\s*item:\s*",
            @"^todo:\s*",
            @"^follow\s*up:\s*",
            @"^create\s+new\s+jira\s+ticket:\s*",
            @"^create\s+jira\s+ticket:\s*",
            @"^new\s+jira\s+ticket:\s*",
            @"^jira\s+ticket:\s*",
            @"^task:\s*",
            @"^assigned\s+to:\s*[^-]+[-:]\s*",
            @"^priority:\s*[^-]+[-:]\s*",
            @"^due\s+date:\s*[^-]+[-:]\s*"
        };

        foreach (var pattern in prefixPatterns)
        {
            cleanLine = Regex.Replace(cleanLine, pattern, "", RegexOptions.IgnoreCase).Trim();
        }

        // Remove quotes if the entire string is quoted
        if (cleanLine.StartsWith("\"") && cleanLine.EndsWith("\""))
        {
            cleanLine = cleanLine.Substring(1, cleanLine.Length - 2).Trim();
        }

        return cleanLine;
    }

    /// <summary>
    /// Determines action item type based on content
    /// </summary>
    private static ActionItemType DetermineActionItemType(string content)
    {
        var lowerContent = content.ToLowerInvariant();

        if (
            lowerContent.Contains("bug")
            || lowerContent.Contains("fix")
            || lowerContent.Contains("error")
        )
            return ActionItemType.Bug;

        if (lowerContent.Contains("investigate") || lowerContent.Contains("research"))
            return ActionItemType.Investigation;

        if (
            lowerContent.Contains("document")
            || lowerContent.Contains("write")
            || lowerContent.Contains("spec")
        )
            return ActionItemType.Documentation;

        if (lowerContent.Contains("review") || lowerContent.Contains("check"))
            return ActionItemType.Review;

        if (lowerContent.Contains("story") || lowerContent.Contains("feature"))
            return ActionItemType.Story;

        return ActionItemType.Task;
    }

    /// <summary>
    /// Parses action items from plain text AI response
    /// </summary>
    private static List<ActionItem> ParseActionItemsFromText(
        string text,
        MeetingTranscript transcript
    )
    {
        // Simple text-based parsing as fallback
        var actionItems = new List<ActionItem>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (IsActionItemLine(line))
            {
                var actionItem = CreateActionItemFromLine(line, transcript);
                if (actionItem != null)
                {
                    actionItems.Add(actionItem);
                }
            }
        }

        return actionItems;
    }

    /// <summary>
    /// Applies business rules to filter and prioritize action items
    /// </summary>
    private static List<ActionItem> ApplyBusinessRules(List<ActionItem> actionItems)
    {
        return actionItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .Where(item => item.Title.Length >= 10) // Minimum meaningful length
            .GroupBy(item => item.Title.ToLowerInvariant())
            .Select(group => group.First()) // Remove duplicates
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.DueDate ?? DateTime.MaxValue)
            .ToList();
    }

    #region Helper Methods

    private static string? ExtractTitle(string firstLine)
    {
        // Remove common prefixes and clean up
        var title = Regex
            .Replace(firstLine, @"^(meeting|transcript|notes?):\s*", "", RegexOptions.IgnoreCase)
            .Trim();
        return string.IsNullOrEmpty(title) ? null : title;
    }

    private static DateTime? ExtractMeetingDate(string content)
    {
        // Look for date patterns
        var datePatterns = new[]
        {
            @"date:\s*(\d{4}-\d{2}-\d{2})",
            @"(\d{1,2}/\d{1,2}/\d{4})",
            @"(\d{4}-\d{2}-\d{2})",
            @"(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4}"
        };

        foreach (var pattern in datePatterns)
        {
            var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
            {
                return date;
            }
        }

        return null;
    }

    private static List<string> ExtractParticipants(string content)
    {
        var participants = new List<string>();

        // Look for participant lists
        var participantPatterns = new[]
        {
            @"participants?:\s*([^\n]+)",
            @"attendees?:\s*([^\n]+)",
            @"present:\s*([^\n]+)"
        };

        foreach (var pattern in participantPatterns)
        {
            var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var names = match.Groups[1].Value
                    .Split(new[] { ',', ';', '&', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => name.Trim())
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();

                participants.AddRange(names);
                break;
            }
        }

        return participants.Distinct().ToList();
    }

    private static string? ExtractProjectKey(string text)
    {
        // Look for Jira project keys
        var pattern = @"\b([A-Z]{2,10})-\d+\b";
        var match = Regex.Match(text, pattern);

        if (match.Success)
        {
            // Extract just the project part (e.g., "OPS" from "OPS-123")
            return match.Groups[1].Value;
        }

        return null;
    }

    #endregion
}
