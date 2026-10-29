using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Drops action items too short to mean anything and merges the ones that say the same thing
/// </summary>
public static class ActionItemMerger
{
    /// <summary>
    /// Items with a title of at least 5 characters, the most urgent first
    /// </summary>
    public static List<ActionItem> ApplyBusinessRules(IEnumerable<ActionItem> actionItems) =>
        actionItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) && item.Title.Length >= 5)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Title)
            .ToList();

    /// <summary>
    /// Merges items with the same or nearly the same title, a title that contains the other,
    /// or the same assignee and nearly the same description
    /// </summary>
    public static List<ActionItem> Deduplicate(List<ActionItem> actionItems)
    {
        var merged = new List<ActionItem>();
        var used = new bool[actionItems.Count];

        for (var i = 0; i < actionItems.Count; i++)
        {
            if (used[i])
                continue;

            var similar = new List<ActionItem> { actionItems[i] };
            for (var j = i + 1; j < actionItems.Count; j++)
            {
                if (!used[j] && AreSimilar(actionItems[i], actionItems[j]))
                {
                    similar.Add(actionItems[j]);
                    used[j] = true;
                }
            }

            merged.Add(similar.Count == 1 ? similar[0] : Merge(similar));
        }

        return merged;
    }

    private static bool AreSimilar(ActionItem first, ActionItem second)
    {
        if (string.Equals(first.Title, second.Title, StringComparison.OrdinalIgnoreCase)
            || Similarity(first.Title, second.Title) > 0.8)
            return true;

        var firstTitle = first.Title.ToLowerInvariant();
        var secondTitle = second.Title.ToLowerInvariant();
        if (firstTitle.Contains(secondTitle) || secondTitle.Contains(firstTitle))
            return true;

        return !string.IsNullOrEmpty(first.AssignedTo)
            && string.Equals(first.AssignedTo, second.AssignedTo, StringComparison.OrdinalIgnoreCase)
            && Similarity(first.Description, second.Description) > 0.7;
    }

    private static ActionItem Merge(List<ActionItem> items)
    {
        // The most detailed item gives the title and the type
        var main = items.OrderByDescending(item => item.Title.Length + item.Description.Length).First();
        var assignees = items
            .Where(item => !string.IsNullOrWhiteSpace(item.AssignedTo))
            .Select(item => item.AssignedTo!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ActionItem
        {
            Id = main.Id,
            Title = main.Title,
            Description = JoinDistinct(items.Select(item => item.Description), "\n\n"),
            AssignedTo = assignees.Count == 0 ? null : string.Join(", ", assignees),
            DueDate = items.FirstOrDefault(item => item.DueDate.HasValue)?.DueDate,
            Priority = items.Max(item => item.Priority),
            Type = main.Type,
            Context = JoinDistinct(items.Select(item => item.Context), "\n"),
            ProjectKey = main.ProjectKey,
            RequiresJiraTicket = items.Any(item => item.RequiresJiraTicket)
        };
    }

    private static string JoinDistinct(IEnumerable<string> texts, string separator) =>
        string.Join(separator, texts
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// 1 for equal strings, 0 for nothing in common (Levenshtein distance over the longer length)
    /// </summary>
    private static double Similarity(string first, string second)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
            return 0;

        var a = first.ToLowerInvariant();
        var b = second.ToLowerInvariant();
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return 1.0 - (double)previous[b.Length] / Math.Max(a.Length, b.Length);
    }
}
