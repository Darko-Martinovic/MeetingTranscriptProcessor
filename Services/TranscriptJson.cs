using System.Text;
using System.Text.Json;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// A transcript in the MS Teams / Graph API JSON layout: meetingTitle, startTime, endTime, locale,
/// participants (displayName) and combinedTranscript (speaker, text, timestamp)
/// </summary>
public sealed record TeamsTranscript(
    string? Title,
    string? StartTime,
    string? EndTime,
    string? Locale,
    IReadOnlyList<string> Participants,
    IReadOnlyList<TeamsSegment> Segments);

/// <summary>
/// One thing said in a Teams transcript
/// </summary>
public sealed record TeamsSegment(string SpeakerId, string Speaker, string Text, string? Timestamp);

/// <summary>
/// Reads JSON transcript files: the Teams layout, or a transcript, content or text property
/// </summary>
public static class TranscriptJson
{
    /// <summary>
    /// The transcript in a JSON file as readable text; JSON in no known layout is returned as it is
    /// </summary>
    public static string ToText(string json, bool includeTimestamps = false, int? maxSegments = null)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (TryReadTeams(root, out var teams))
            {
                return Format(teams, includeTimestamps, maxSegments);
            }

            foreach (var name in new[] { "transcript", "content", "text" })
            {
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value))
                {
                    return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.GetRawText();
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON after all: read as plain text
        }

        return json;
    }

    /// <summary>
    /// Reads a transcript in the Teams layout
    /// </summary>
    public static bool TryReadTeams(string json, out TeamsTranscript transcript)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return TryReadTeams(document.RootElement, out transcript);
        }
        catch (JsonException)
        {
            transcript = null!;
            return false;
        }
    }

    private static bool TryReadTeams(JsonElement root, out TeamsTranscript transcript)
    {
        transcript = null!;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("combinedTranscript", out var segments) || segments.ValueKind != JsonValueKind.Array
            || !root.TryGetProperty("participants", out var participants) || participants.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        transcript = new TeamsTranscript(
            Text(root, "meetingTitle"),
            Text(root, "startTime"),
            Text(root, "endTime"),
            Text(root, "locale"),
            participants.EnumerateArray()
                .Select(participant => Text(participant, "displayName"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList(),
            segments.EnumerateArray()
                .Select(segment =>
                {
                    var speaker = segment.ValueKind == JsonValueKind.Object && segment.TryGetProperty("speaker", out var s) ? s : default;
                    return new TeamsSegment(
                        Text(speaker, "id") ?? "",
                        Text(speaker, "displayName") ?? "Unknown Speaker",
                        Text(segment, "text") ?? "",
                        Text(segment, "timestamp"));
                })
                .ToList());
        return true;
    }

    private static string Format(TeamsTranscript transcript, bool includeTimestamps, int? maxSegments)
    {
        var text = new StringBuilder();
        text.Append("MEETING TRANSCRIPT\n=================\n");
        AppendIfPresent(text, "Title", transcript.Title);
        AppendIfPresent(text, "Date", transcript.StartTime);
        AppendIfPresent(text, "End", transcript.EndTime);
        AppendIfPresent(text, "Language", transcript.Locale);

        if (transcript.Participants.Count > 0)
        {
            text.Append("Participants:\n");
            foreach (var participant in transcript.Participants)
            {
                text.Append("- ").Append(participant).Append('\n');
            }
        }

        text.Append("\n--------------------------------------------------------------\n\n");

        var spoken = transcript.Segments
            .Where(segment => segment.SpeakerId != "system" && !string.IsNullOrWhiteSpace(segment.Text))
            .ToList();
        foreach (var segment in spoken.Take(maxSegments ?? int.MaxValue))
        {
            if (includeTimestamps && !string.IsNullOrWhiteSpace(segment.Timestamp))
            {
                text.Append('[').Append(segment.Timestamp).Append("] ");
            }

            text.Append(segment.Speaker).Append(": ").Append(segment.Text).Append("\n\n");
        }

        if (maxSegments == null || spoken.Count <= maxSegments)
        {
            text.Append("--------------------------------------------------------------\n(End of Transcript)");
        }

        return text.ToString();
    }

    private static void AppendIfPresent(StringBuilder text, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            text.Append(label).Append(": ").Append(value).Append('\n');
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Null => null,
                _ => value.GetRawText()
            }
            : null;
}
