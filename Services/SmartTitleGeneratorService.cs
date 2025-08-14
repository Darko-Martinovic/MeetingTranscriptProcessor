using System.Text.RegularExpressions;
using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services
{
    /// <summary>
    /// Service for generating intelligent meeting titles based on action items content
    /// </summary>
    public class SmartTitleGeneratorService
    {
        private readonly ILogger<SmartTitleGeneratorService>? _logger;

        public SmartTitleGeneratorService(ILogger<SmartTitleGeneratorService>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Checks if a title is generic and should be replaced
        /// </summary>
        public bool IsGenericTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return true;

            var genericPatterns = new[]
            {
                @"^meeting transcript$",
                @"^meeting$",
                @"^transcript$",
                @"^team meeting$",
                @"^daily standup$",
                @"^standup$",
                @"^sync meeting$",
                @"^sync$",
                @"^call$",
                @"^meeting notes$",
                @"^notes$",
                @"^discussion$",
                @"^weekly meeting$",
                @"^monthly meeting$",
                @"^status meeting$",
                @"^status update$",
                @"^update meeting$",
                @".*test.*meeting.*",
                @".*monitor.*test.*",
                @".*processing.*test.*",
                @"^test.*",
                @"^sample.*",
                @"^demo.*",
                @"^untitled.*",
                @"^new.*meeting.*",
                @"^meeting\s*-?\s*\d+$",
                @"^meeting\s+\d{4}-\d{2}-\d{2}$"
            };

            var normalizedTitle = title.Trim().ToLowerInvariant();

            return genericPatterns.Any(pattern =>
                Regex.IsMatch(normalizedTitle, pattern, RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Generates a smart title based on action items content
        /// </summary>
        public string GenerateSmartTitle(List<ActionItem> actionItems, MeetingTranscript transcript)
        {
            try
            {
                if (actionItems == null || actionItems.Count == 0)
                {
                    return GenerateFallbackTitle(transcript);
                }

                // Extract key themes and topics from action items
                var themes = ExtractThemes(actionItems);
                var participants = transcript.Participants?.Take(3).ToList() ?? new List<string>();
                var meetingDate = transcript.MeetingDate;

                // Generate title based on themes
                var smartTitle = GenerateTitleFromThemes(themes, participants, meetingDate);

                _logger?.LogInformation($"Generated smart title: '{smartTitle}' from {actionItems.Count} action items");

                return smartTitle;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating smart title, using fallback");
                return GenerateFallbackTitle(transcript);
            }
        }

        /// <summary>
        /// Extracts key themes and topics from action items
        /// </summary>
        private List<string> ExtractThemes(List<ActionItem> actionItems)
        {
            var themes = new List<string>();
            var keywordGroups = new Dictionary<string, List<string>>
            {
                ["Development"] = new() { "implement", "develop", "code", "build", "create", "setup", "configure", "deploy", "release", "feature", "bug", "fix", "testing", "test", "qa", "review", "merge", "commit", "api", "database", "frontend", "backend", "ui", "ux" },
                ["Planning"] = new() { "plan", "schedule", "organize", "prepare", "design", "strategy", "roadmap", "timeline", "milestone", "sprint", "backlog", "estimate", "scope", "requirements", "specification", "architecture" },
                ["Meeting"] = new() { "meeting", "call", "sync", "standup", "retrospective", "demo", "presentation", "discussion", "brainstorm", "workshop", "training", "onboarding" },
                ["Documentation"] = new() { "document", "write", "update", "create", "documentation", "readme", "wiki", "guide", "manual", "specs", "notes", "report", "summary" },
                ["Review"] = new() { "review", "approve", "feedback", "evaluate", "assess", "check", "validate", "verify", "audit", "inspect", "analyze" },
                ["Coordination"] = new() { "coordinate", "communicate", "inform", "update", "notify", "follow up", "followup", "reach out", "contact", "escalate", "delegate", "assign" },
                ["Research"] = new() { "research", "investigate", "analyze", "study", "explore", "learn", "understand", "benchmark", "compare", "evaluate options" },
                ["Budget"] = new() { "budget", "cost", "expense", "financial", "funding", "payment", "invoice", "billing", "price", "estimate cost" },
                ["Hiring"] = new() { "hire", "recruit", "interview", "onboard", "training", "team", "staff", "resource", "headcount", "candidate" },
                ["Infrastructure"] = new() { "server", "cloud", "aws", "azure", "deployment", "ci/cd", "pipeline", "monitoring", "security", "backup", "performance" }
            };

            // Count occurrences of keywords in action items
            var themeCounts = new Dictionary<string, int>();

            foreach (var actionItem in actionItems)
            {
                var text = $"{actionItem.Title} {actionItem.Description}".ToLowerInvariant();

                foreach (var group in keywordGroups)
                {
                    var matchCount = group.Value.Count(keyword => text.Contains(keyword));
                    if (matchCount > 0)
                    {
                        themeCounts[group.Key] = themeCounts.GetValueOrDefault(group.Key, 0) + matchCount;
                    }
                }
            }

            // Return top themes
            return themeCounts
                .OrderByDescending(x => x.Value)
                .Take(2)
                .Select(x => x.Key)
                .ToList();
        }

        /// <summary>
        /// Generates a title from extracted themes
        /// </summary>
        private string GenerateTitleFromThemes(List<string> themes, List<string> participants, DateTime meetingDate)
        {
            if (themes.Count == 0)
            {
                return GenerateGenericTitle(participants, meetingDate);
            }

            var title = "";

            // Single theme
            if (themes.Count == 1)
            {
                title = themes[0] switch
                {
                    "Development" => "Development Planning Meeting",
                    "Planning" => "Sprint Planning Session",
                    "Meeting" => "Team Sync Meeting",
                    "Documentation" => "Documentation Review",
                    "Review" => "Review & Approval Session",
                    "Coordination" => "Team Coordination Meeting",
                    "Research" => "Research & Analysis Session",
                    "Budget" => "Budget Planning Meeting",
                    "Hiring" => "Hiring & Recruitment Meeting",
                    "Infrastructure" => "Infrastructure Planning",
                    _ => $"{themes[0]} Meeting"
                };
            }
            // Multiple themes
            else if (themes.Count >= 2)
            {
                var combinedThemes = string.Join(" & ", themes.Take(2));
                title = $"{combinedThemes} Session";

                // Special combinations
                if (themes.Contains("Development") && themes.Contains("Planning"))
                    title = "Sprint Planning Meeting";
                else if (themes.Contains("Review") && themes.Contains("Development"))
                    title = "Code Review Session";
                else if (themes.Contains("Planning") && themes.Contains("Budget"))
                    title = "Budget Planning Meeting";
                else if (themes.Contains("Meeting") && themes.Contains("Coordination"))
                    title = "Team Coordination Meeting";
            }

            // Add date context if recent
            if (meetingDate.Date == DateTime.Today)
                title += " - Today";
            else if (meetingDate.Date == DateTime.Today.AddDays(-1))
                title += " - Yesterday";
            else if (meetingDate >= DateTime.Today.AddDays(-7))
                title += $" - {meetingDate:MMM dd}";

            return title;
        }

        /// <summary>
        /// Generates a generic but still meaningful title
        /// </summary>
        private string GenerateGenericTitle(List<string> participants, DateTime meetingDate)
        {
            var title = "Team Meeting";

            if (participants.Count > 0)
            {
                if (participants.Count <= 2)
                    title = $"{string.Join(" & ", participants)} Meeting";
                else if (participants.Count == 3)
                    title = $"{string.Join(", ", participants)} Meeting";
                else
                    title = $"Team Meeting ({participants.Count} participants)";
            }

            // Add date if not today
            if (meetingDate.Date != DateTime.Today)
            {
                if (meetingDate.Date == DateTime.Today.AddDays(-1))
                    title += " - Yesterday";
                else if (meetingDate >= DateTime.Today.AddDays(-7))
                    title += $" - {meetingDate:MMM dd}";
                else
                    title += $" - {meetingDate:yyyy-MM-dd}";
            }

            return title;
        }

        /// <summary>
        /// Generates a fallback title when no action items are available
        /// </summary>
        private string GenerateFallbackTitle(MeetingTranscript transcript)
        {
            var participants = transcript.Participants?.Take(3).ToList() ?? new List<string>();
            var meetingDate = transcript.MeetingDate;

            return GenerateGenericTitle(participants, meetingDate);
        }

        /// <summary>
        /// Updates meeting title if it's generic
        /// </summary>
        public void UpdateTitleIfGeneric(MeetingTranscript transcript)
        {
            if (IsGenericTitle(transcript.Title))
            {
                var originalTitle = transcript.Title;
                transcript.Title = GenerateSmartTitle(transcript.ActionItems, transcript);

                _logger?.LogInformation($"Updated generic title '{originalTitle}' to '{transcript.Title}'");
            }
        }
    }
}
