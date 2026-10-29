using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Reads a meeting file of any supported format as text, with what the text says about the meeting:
/// title, date, participants and Jira project. Nothing here calls a model
/// </summary>
public class TranscriptReader
{
    private static readonly Regex XmlTextPattern = new(
        @"<(?:text|content|transcript)[^>]*>(.*?)</(?:text|content|transcript)>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly HashSet<string> MetadataFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Date", "Time", "Location", "Participants", "Attendees", "Meeting", "Subject", "Agenda",
        "Datum", "Tijd", "Locatie", "Deelnemers", "Aanwezigen", "Vergadering", "Onderwerp",
        "Vergadertype", "Taal", "Type", "Language", "Meeting Type"
    };

    private readonly WebVttParserService _vttParser = new();
    private readonly IConfiguration _configuration;
    private readonly ILogger<TranscriptReader> _logger;

    public TranscriptReader(IConfiguration configuration, ILogger<TranscriptReader> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// The file extensions the app reads
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".json", ".xml", ".docx", ".pdf", ".vtt" };

    public static bool IsSupported(string fileName) => SupportedExtensions.Contains(Path.GetExtension(fileName));

    /// <summary>
    /// Reads a meeting file with its title, date, participants and project key
    /// </summary>
    public async Task<MeetingTranscript> ReadAsync(string filePath)
    {
        var transcript = new MeetingTranscript
        {
            FileName = Path.GetFileName(filePath),
            Content = await ReadTextAsync(filePath)
        };

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".json" && TranscriptJson.TryReadTeams(await File.ReadAllTextAsync(filePath), out var teams))
        {
            transcript.Title = string.IsNullOrWhiteSpace(teams.Title)
                ? Path.GetFileNameWithoutExtension(transcript.FileName)
                : teams.Title;
            transcript.MeetingDate = DateTime.TryParse(teams.StartTime, out var start) ? start.Date : DateTime.UtcNow.Date;
            transcript.Participants = teams.Participants.ToList();
            if (!string.IsNullOrWhiteSpace(teams.Locale))
            {
                transcript.DetectedLanguage = MapLocaleToLanguageCode(teams.Locale);
            }
        }
        else
        {
            // The first line of a WebVTT, Word or PDF transcript is usually someone speaking, so those get a
            // generic title that SmartTitleGeneratorService replaces
            var firstLine = transcript.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            transcript.Title = extension is ".vtt" or ".docx" or ".pdf" || firstLine == null
                ? "Meeting Transcript"
                : ExtractTitle(firstLine) ?? Path.GetFileNameWithoutExtension(transcript.FileName);
            transcript.MeetingDate = ExtractMeetingDate(transcript.Content) ?? DateTime.UtcNow.Date;
            transcript.Participants = ExtractParticipants(transcript.Content);
        }

        transcript.ProjectKey = ExtractProjectKey(transcript.FileName, transcript.Content, DefaultProjectKey);

        _logger.LogDebug(
            "Read {FileName}: title {Title}, {Participants} participants",
            transcript.FileName, transcript.Title, transcript.Participants.Count);
        return transcript;
    }

    /// <summary>
    /// The text of a meeting file; a file that can't be parsed in its format is read as plain text
    /// </summary>
    public async Task<string> ReadTextAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".json" => TranscriptJson.ToText(await File.ReadAllTextAsync(filePath)),
            ".xml" => ReadXml(await File.ReadAllTextAsync(filePath)),
            ".vtt" => ReadVtt(await File.ReadAllTextAsync(filePath)),
            ".docx" => ReadDocx(filePath),
            _ => await File.ReadAllTextAsync(filePath)
        };
    }

    private string DefaultProjectKey =>
        _configuration["JIRA_PROJECT_KEY"] is { Length: > 0 } key ? key
        : _configuration["JIRA_DEFAULT_PROJECT"] is { Length: > 0 } project ? project
        : "TASK";

    private static string ReadXml(string xml)
    {
        var matches = XmlTextPattern.Matches(xml);
        return matches.Count > 0
            ? string.Join("\n", matches.Select(match => match.Groups[1].Value))
            : Regex.Replace(xml, @"<[^>]+>", " ");
    }

    private string ReadVtt(string vtt)
    {
        var text = _vttParser.ParseWebVtt(vtt);
        return string.IsNullOrWhiteSpace(text) ? vtt : text;
    }

    private string ReadDocx(string filePath)
    {
        try
        {
            using var document = WordprocessingDocument.Open(filePath, false);
            var body = document.MainDocumentPart?.Document?.Body;
            if (body == null)
            {
                return string.Empty;
            }

            // One line per paragraph, so participant lists and speaker lines stay apart
            return string.Join("\n", body.Descendants<Paragraph>()
                .Select(paragraph => paragraph.InnerText)
                .Where(text => !string.IsNullOrWhiteSpace(text)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the Word file {FileName}", Path.GetFileName(filePath));
            return $"[Error reading DOCX file: {ex.Message}]";
        }
    }

    private static string MapLocaleToLanguageCode(string locale) => locale.ToLowerInvariant() switch
    {
        "en-us" or "en-gb" or "en-ca" or "en-au" => "en",
        "fr-fr" or "fr-ca" => "fr",
        "nl-nl" or "nl-be" => "nl",
        "de-de" or "de-at" => "de",
        "es-es" or "es-mx" => "es",
        _ => locale.Split('-')[0]
    };

    private static string? ExtractTitle(string firstLine)
    {
        var title = firstLine.Trim().TrimStart('#').Trim();
        title = Regex.Replace(title, @"^(meeting|call|standup|sync):\s*", "", RegexOptions.IgnoreCase);
        return string.IsNullOrEmpty(title) ? null : title;
    }

    private static DateTime? ExtractMeetingDate(string content)
    {
        var datePatterns = new[]
        {
            @"date:\s*(\d{4}-\d{2}-\d{2})",
            @"(\d{1,2}/\d{1,2}/\d{4})",
            @"(\d{4}-\d{2}-\d{2})",
            @"((?:january|february|march|april|may|june|july|august|september|october|november|december)\s+\d{1,2},?\s+\d{4})"
        };

        foreach (var pattern in datePatterns)
        {
            var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var date))
            {
                return date;
            }
        }

        return null;
    }

    /// <summary>
    /// The participants: a bulleted list or a line after Participants:, Attendees: or the Dutch and
    /// French words for them; otherwise the names of the speakers
    /// </summary>
    private static List<string> ExtractParticipants(string content)
    {
        var bulleted = Regex.Match(
            content,
            @"(?:participants?|attendees?|deelnemers?|aanwezigen?):\s*\n((?:\s*[-•]\s*[^\n]+(?:\n|$))+)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (bulleted.Success)
        {
            var names = bulleted.Groups[1].Value.Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.StartsWith('-') || line.StartsWith('•'))
                .Select(line => WithoutRole(line[1..]))
                .Where(name => name.Length > 0)
                .Distinct()
                .ToList();
            if (names.Count > 0)
            {
                return names;
            }
        }

        var line = Regex.Match(
            content,
            @"(?:participants?|attendees?|present|deelnemers?|aanwezigen?|présents)\s*:\s*([^\n]+)",
            RegexOptions.IgnoreCase);
        if (line.Success)
        {
            var names = line.Groups[1].Value.Split(',', ';')
                .Select(WithoutRole)
                .Where(name => name.Length > 0)
                .Distinct()
                .ToList();
            if (names.Count > 0)
            {
                return names;
            }
        }

        // "Name: text", or "Name HH:MM:SS.mmm" as converted WebVTT has it
        var speakerPatterns = new[]
        {
            @"^([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*):\s*(.+)$",
            @"^([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)\s+\d{2}:\d{2}:\d{2}\.\d{3}$"
        };
        return speakerPatterns
            .SelectMany(pattern => Regex.Matches(content, pattern, RegexOptions.Multiline))
            .Select(match => match.Groups[1].Value.Trim())
            .Where(name => !MetadataFields.Contains(name))
            .Distinct()
            .Take(10)
            .ToList();
    }

    // "Ann (Product Owner - Extern)" is Ann
    private static string WithoutRole(string name) => Regex.Replace(name, @"\s*\([^)]*\)", "").Trim();

    /// <summary>
    /// A project named in the content (Project: KEY, or an issue key like KEY-123), then an upper-case
    /// word in the file name, then the default
    /// </summary>
    private static string ExtractProjectKey(string fileName, string content, string defaultProjectKey)
    {
        var named = Regex.Match(content, @"project(?:[_\s]key)?:\s*([A-Z]{2,10})\b", RegexOptions.IgnoreCase);
        if (named.Success)
        {
            return named.Groups[1].Value.ToUpperInvariant();
        }

        // Jira keys are upper case, so "covid-19" is not an issue key
        var issueKey = Regex.Match(content, @"\b([A-Z]{2,10})-\d+\b");
        if (issueKey.Success)
        {
            return issueKey.Groups[1].Value;
        }

        var fromFileName = Regex.Match(fileName, @"([A-Z]{2,10})");
        return fromFileName.Success ? fromFileName.Groups[1].Value : defaultProjectKey;
    }
}
