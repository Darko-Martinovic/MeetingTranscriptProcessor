using System.Text.RegularExpressions;
using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Finds action items line by line with the keywords and patterns of the extraction settings
/// (config/extraction.json), without a model
/// </summary>
public static class RuleBasedExtractor
{
    /// <summary>
    /// The action items in a text, one per matching line
    /// </summary>
    public static List<ActionItem> Extract(string text, string? projectKey, ExtractionSettings settings)
    {
        var keywordsPattern = string.Join("|", settings.ActionKeywords.Select(Regex.Escape));
        var actionPatterns = settings.ActionPatterns
            .Select(pattern => pattern.Replace("{keywords}", keywordsPattern))
            .ToArray();

        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => actionPatterns.Any(pattern => Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase)))
            .Select(line => FromLine(line, projectKey, settings))
            .OfType<ActionItem>()
            .ToList();
    }

    private static ActionItem? FromLine(string line, string? projectKey, ExtractionSettings settings)
    {
        var title = CleanTitle(line, settings);
        if (string.IsNullOrEmpty(title))
        {
            return null;
        }

        return new ActionItem
        {
            Title = title.Length > settings.MaxTitleLength ? title[..settings.MaxTitleLength] + "..." : title,
            Description = title,
            Context = line,
            ProjectKey = projectKey,
            Priority = ActionItemPriority.Medium,
            Type = TypeOf(title, settings)
        };
    }

    private static string CleanTitle(string line, ExtractionSettings settings)
    {
        var title = line.Trim();
        foreach (var prefix in settings.TitlePrefixes)
        {
            title = Regex.Replace(title, prefix, "", RegexOptions.IgnoreCase).Trim();
        }

        return title.Length >= 2 && title.StartsWith('"') && title.EndsWith('"') ? title[1..^1].Trim() : title;
    }

    private static ActionItemType TypeOf(string text, ExtractionSettings settings)
    {
        var lower = text.ToLowerInvariant();
        if (settings.BugKeywords.Any(lower.Contains)) return ActionItemType.Bug;
        if (settings.InvestigationKeywords.Any(lower.Contains)) return ActionItemType.Investigation;
        if (settings.DocumentationKeywords.Any(lower.Contains)) return ActionItemType.Documentation;
        if (settings.ReviewKeywords.Any(lower.Contains)) return ActionItemType.Review;
        if (settings.StoryKeywords.Any(lower.Contains)) return ActionItemType.Story;
        return ActionItemType.Task;
    }
}
