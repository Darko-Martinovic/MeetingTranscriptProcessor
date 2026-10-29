using System.Text.Json;
using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Reads the action items from the model's answer: JSON with an actionItems array,
/// possibly inside a ```json block
/// </summary>
public static class AiResponseParser
{
    /// <summary>
    /// The action items in the answer, or null when the answer isn't JSON in that shape
    /// </summary>
    public static List<ActionItem>? Parse(string response, string? projectKey)
    {
        try
        {
            using var document = JsonDocument.Parse(WithoutCodeFence(response));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("actionItems", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return items.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => new ActionItem
                {
                    Title = Text(item, "title") ?? "Untitled Action Item",
                    Description = Text(item, "description") ?? "",
                    AssignedTo = Text(item, "assignedTo"),
                    Context = Text(item, "context") ?? "",
                    ProjectKey = projectKey,
                    RequiresJiraTicket = !item.TryGetProperty("requiresJiraTicket", out var requires)
                        || requires.ValueKind != JsonValueKind.False,
                    Priority = Enum.TryParse<ActionItemPriority>(Text(item, "priority"), ignoreCase: true, out var priority)
                        ? priority
                        : ActionItemPriority.Medium,
                    Type = Enum.TryParse<ActionItemType>(Text(item, "type"), ignoreCase: true, out var type)
                        ? type
                        : ActionItemType.Task,
                    DueDate = DateTime.TryParse(Text(item, "dueDate"), System.Globalization.CultureInfo.InvariantCulture, out var dueDate)
                        ? dueDate
                        : null
                })
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The text without the ``` or ```json fence a model often puts around JSON
    /// </summary>
    public static string WithoutCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstLineEnd = trimmed.IndexOf('\n');
            trimmed = firstLineEnd < 0 ? trimmed[3..] : trimmed[(firstLineEnd + 1)..];
            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed[..^3];
            }
        }

        return trimmed.Trim();
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
            : null;
}
