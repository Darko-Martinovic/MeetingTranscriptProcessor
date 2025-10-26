using System.Text;
using System.Text.RegularExpressions;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Service for parsing WebVTT (Web Video Text Tracks) format files.
/// Extracts speakers and content from MS Teams-style meeting recording transcripts.
/// </summary>
public class WebVttParserService
{
    private static readonly Regex SpeakerTagRegex = new Regex(
        @"<v\s+([^>]+)>([^<]+)</v>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private static readonly Regex TimestampRegex = new Regex(
        @"^\d{2}:\d{2}:\d{2}\.\d{3}\s*-->\s*\d{2}:\d{2}:\d{2}\.\d{3}$",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Parses WebVTT content and converts it to a readable transcript format.
    /// </summary>
    /// <param name="vttContent">The raw WebVTT file content</param>
    /// <returns>Converted transcript with speaker names and dialog</returns>
    public string ParseWebVtt(string vttContent)
    {
        if (string.IsNullOrWhiteSpace(vttContent))
        {
            return string.Empty;
        }

        var lines = vttContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var result = new StringBuilder();
        var inHeader = true;

        foreach (var line in lines)
        {
            // Skip WEBVTT header
            if (inHeader)
            {
                if (line.Trim().StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(line))
                {
                    inHeader = false;
                    continue;
                }
            }

            // Skip timestamp lines
            if (TimestampRegex.IsMatch(line.Trim()))
            {
                continue;
            }

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Extract speaker and text from <v Name>Text</v> format
            var matches = SpeakerTagRegex.Matches(line);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    var speaker = match.Groups[1].Value.Trim();
                    var text = match.Groups[2].Value.Trim();

                    result.AppendLine($"{speaker}: {text}");
                    result.AppendLine();
                }
            }
            else
            {
                // If no speaker tag, append line as-is (shouldn't happen in valid VTT)
                result.AppendLine(line.Trim());
                result.AppendLine();
            }
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// Extracts all unique speaker names from WebVTT content.
    /// </summary>
    /// <param name="vttContent">The raw WebVTT file content</param>
    /// <returns>List of unique speaker names in alphabetical order</returns>
    public List<string> ExtractSpeakers(string vttContent)
    {
        if (string.IsNullOrWhiteSpace(vttContent))
        {
            return new List<string>();
        }

        var speakers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matches = SpeakerTagRegex.Matches(vttContent);

        foreach (Match match in matches)
        {
            var speaker = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(speaker))
            {
                speakers.Add(speaker);
            }
        }

        return speakers.OrderBy(s => s).ToList();
    }

    /// <summary>
    /// Checks if content is in WebVTT format.
    /// </summary>
    /// <param name="content">The file content to check</param>
    /// <returns>True if content appears to be WebVTT format</returns>
    public bool IsWebVtt(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        // Check if first non-empty line starts with WEBVTT
        var firstLine = content.TrimStart().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstLine?.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase) == true;
    }
}
