using System.Globalization;
using System.Text.Json;
using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Filters and sorting for a list of meetings; a comma-separated value matches any of its parts
/// </summary>
public sealed record MeetingQuery(
    string? Search = null,
    string? Status = null,
    string? Language = null,
    string? Participants = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    bool? HasJiraTickets = null,
    string SortBy = "date",
    string SortOrder = "desc",
    string? FavoriteFileNames = null);

/// <summary>
/// What became of a request to change a meeting file: an error for the caller, or done
/// </summary>
public sealed record MeetingChange(bool Found, string? Error = null, string? FileName = null, string? Message = null)
{
    public static MeetingChange NotFound { get; } = new(false);
}

/// <summary>
/// The meeting files in the incoming, processing and archive folders and the metadata saved beside
/// the archived ones. Every file name that comes from a request must pass <see cref="MeetingFileNames"/>
/// </summary>
public class MeetingStore
{
    private const int RecentCount = 5;

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string[] TextExtensions = [".txt", ".md"];

    private readonly StoragePaths _paths;
    private readonly TranscriptReader _reader;
    private readonly ILogger<MeetingStore> _logger;

    public MeetingStore(StoragePaths paths, TranscriptReader reader, ILogger<MeetingStore> logger)
    {
        _paths = paths;
        _reader = reader;
        _logger = logger;
    }

    /// <summary>
    /// The folders with their meeting counts; Favorites are kept in the browser, so its count is 0
    /// </summary>
    public async Task<List<FolderInfo>> GetFoldersAsync() =>
    [
        new() { Name = "Archive", Path = _paths.Archive, Type = FolderType.Archive, MeetingCount = MeetingFiles(_paths.Archive).Count() },
        new() { Name = "Incoming", Path = _paths.Incoming, Type = FolderType.Incoming, MeetingCount = MeetingFiles(_paths.Incoming).Count() },
        new() { Name = "Processing", Path = _paths.Processing, Type = FolderType.Processing, MeetingCount = MeetingFiles(_paths.Processing).Count() },
        new() { Name = "Recent", Path = "", Type = FolderType.Recent, MeetingCount = (await GetRecentAsync()).Count },
        new() { Name = "Favorites", Path = "", Type = FolderType.Favorites, MeetingCount = 0 }
    ];

    /// <summary>
    /// The meetings in a folder, filtered and sorted
    /// </summary>
    public async Task<List<MeetingInfo>> GetMeetingsAsync(FolderType folder, MeetingQuery query)
    {
        var meetings = folder switch
        {
            FolderType.Recent => await GetRecentAsync(),
            FolderType.Favorites => await GetFavoritesAsync(query.FavoriteFileNames),
            _ => await GetFromFolderAsync(FolderPath(folder)!, folder)
        };

        return Sort(Filter(meetings, query), query.SortBy, query.SortOrder);
    }

    /// <summary>
    /// A meeting with what processing found (from its metadata), or as read from the file when it
    /// hasn't been processed yet; null when there's no such file
    /// </summary>
    public async Task<MeetingTranscript?> GetMeetingAsync(string fileName)
    {
        var metadata = await LoadMetadataAsync(fileName);
        if (metadata != null)
        {
            return metadata;
        }

        var found = Find(fileName);
        return found == null ? null : await _reader.ReadAsync(found.Value.Path);
    }

    /// <summary>
    /// The Jira tickets created for a meeting
    /// </summary>
    public async Task<List<JiraTicketReference>> GetTicketsAsync(string fileName) =>
        (await LoadMetadataAsync(fileName))?.CreatedJiraTickets ?? [];

    /// <summary>
    /// Saves an upload in the incoming folder under a timestamped name, which it returns
    /// </summary>
    public async Task<string> AddToIncomingAsync(string originalName, Stream content)
    {
        var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{originalName}";
        var filePath = MeetingFileNames.PathIn(_paths.Incoming, fileName)
            ?? throw new ArgumentException("Invalid file name", nameof(originalName));

        await using (var stream = new FileStream(filePath, FileMode.CreateNew))
        {
            await content.CopyToAsync(stream);
        }

        _logger.LogInformation("Uploaded {FileName}", fileName);
        return fileName;
    }

    /// <summary>
    /// Deletes a meeting file and its metadata
    /// </summary>
    public bool Delete(string fileName)
    {
        var found = Find(fileName);
        if (found == null)
        {
            return false;
        }

        File.Delete(found.Value.Path);
        var metadataPath = Path.Combine(Path.GetDirectoryName(found.Value.Path)!, ArchiveNames.MetadataName(fileName));
        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
        }

        _logger.LogInformation("Deleted {FileName}", fileName);
        return true;
    }

    /// <summary>
    /// Moves a meeting to the archive or the incoming folder (a file moved to incoming is processed again)
    /// </summary>
    public MeetingChange Move(string fileName, FolderType target)
    {
        if (target is FolderType.Recent or FolderType.Favorites)
            return new MeetingChange(true, $"Cannot move meetings to {target} folder (it's virtual)");
        if (target == FolderType.Processing)
            return new MeetingChange(true, "Cannot move meetings to Processing folder (it's system-managed)");

        var found = Find(fileName);
        if (found == null)
            return MeetingChange.NotFound;
        if (found.Value.Folder == target)
            return new MeetingChange(true, "Meeting is already in the target folder");

        var targetFolder = FolderPath(target)!;
        var targetName = fileName;
        for (var counter = 1; File.Exists(Path.Combine(targetFolder, targetName)); counter++)
        {
            targetName = $"{Path.GetFileNameWithoutExtension(fileName)}_{counter}{Path.GetExtension(fileName)}";
        }

        File.Move(found.Value.Path, Path.Combine(targetFolder, targetName));
        return new MeetingChange(
            true,
            FileName: targetName,
            Message: $"Meeting moved successfully from {found.Value.Folder} to {target}");
    }

    /// <summary>
    /// Changes a meeting's title: in its metadata when it has some, otherwise in the first line of a
    /// text file. Other files are left alone, since rewriting them as text would break them
    /// </summary>
    public async Task<MeetingChange> UpdateTitleAsync(string fileName, string title)
    {
        var found = Find(fileName);
        if (found == null)
            return MeetingChange.NotFound;

        var metadataPath = FindMetadataPath(fileName);
        if (metadataPath != null)
        {
            var transcript = JsonSerializer.Deserialize<MeetingTranscript>(await File.ReadAllTextAsync(metadataPath), ReadOptions)!;
            transcript.Title = title;
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(transcript, WriteOptions));
            return new MeetingChange(true, Message: "Meeting title updated successfully");
        }

        if (!TextExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
            return new MeetingChange(true, "The title of an unprocessed meeting can only be changed in a .txt or .md file");

        // The first line is replaced when it looks like a title, otherwise the title goes above it
        var lines = (await File.ReadAllTextAsync(found.Value.Path)).Split('\n').ToList();
        var firstLine = lines[0].Trim();
        var firstLineIsTitle = firstLine.Length is > 0 and <= 200
            && !new[] { "Date:", "Time:", "Participants:", "Meeting ID:" }.Any(firstLine.StartsWith);
        if (firstLineIsTitle)
            lines[0] = title;
        else
            lines.Insert(0, title);

        await File.WriteAllTextAsync(found.Value.Path, string.Join('\n', lines));
        return new MeetingChange(true, Message: "Meeting title updated successfully");
    }

    /// <summary>
    /// Moves a processed file to the archive, named after its status and language; returns the new path,
    /// or null when it couldn't be moved
    /// </summary>
    public string? Archive(string filePath, string status, string? languageCode = null)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            var archivedName = ArchiveNames.ArchivedName(Path.GetFileName(filePath), status, languageCode, DateTime.Now);
            var archivedPath = Path.Combine(_paths.Archive, archivedName);
            File.Move(filePath, archivedPath);
            _logger.LogInformation("Archived {ArchivedName}", archivedName);
            return archivedPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to archive {FileName}", Path.GetFileName(filePath));
            return null;
        }
    }

    /// <summary>
    /// Saves what processing found beside the archived file
    /// </summary>
    public Task SaveMetadataAsync(MeetingTranscript transcript, string archivedFilePath) =>
        File.WriteAllTextAsync(
            Path.Combine(Path.GetDirectoryName(archivedFilePath)!, ArchiveNames.MetadataName(Path.GetFileName(archivedFilePath))),
            JsonSerializer.Serialize(transcript, WriteOptions));

    private string? FolderPath(FolderType folder) => folder switch
    {
        FolderType.Archive => _paths.Archive,
        FolderType.Incoming => _paths.Incoming,
        FolderType.Processing => _paths.Processing,
        _ => null
    };

    private (string Path, FolderType Folder)? Find(string fileName)
    {
        foreach (var folder in new[] { FolderType.Archive, FolderType.Incoming, FolderType.Processing })
        {
            var path = MeetingFileNames.PathIn(FolderPath(folder)!, fileName);
            if (path != null && File.Exists(path))
            {
                return (path, folder);
            }
        }

        return null;
    }

    private string? FindMetadataPath(string fileName)
    {
        if (!MeetingFileNames.IsSafe(fileName))
            return null;

        var metadataName = ArchiveNames.MetadataName(fileName);
        return _paths.MeetingFolders
            .Select(folder => Path.Combine(folder, metadataName))
            .FirstOrDefault(File.Exists);
    }

    private async Task<MeetingTranscript?> LoadMetadataAsync(string fileName)
    {
        var metadataPath = FindMetadataPath(fileName);
        if (metadataPath == null)
            return null;

        try
        {
            var transcript = JsonSerializer.Deserialize<MeetingTranscript>(await File.ReadAllTextAsync(metadataPath), ReadOptions);

            // Metadata saved before JSON transcripts were read as text holds the JSON itself
            if (transcript != null && transcript.Content.TrimStart().StartsWith('{'))
            {
                transcript.Content = TranscriptJson.ToText(transcript.Content, includeTimestamps: true);
            }

            return transcript;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "Could not read the metadata of {FileName}", fileName);
            return null;
        }
    }

    private static IEnumerable<string> MeetingFiles(string folder) =>
        Directory.Exists(folder)
            ? Directory.GetFiles(folder)
                .Where(file => TranscriptReader.IsSupported(file)
                    && !Path.GetFileName(file).EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase))
            : [];

    private async Task<List<MeetingInfo>> GetFromFolderAsync(string folder, FolderType folderType)
    {
        var meetings = new List<MeetingInfo>();
        foreach (var file in MeetingFiles(folder).OrderByDescending(File.GetLastWriteTime))
        {
            meetings.Add(await DescribeAsync(file, folderType));
        }

        return meetings;
    }

    private async Task<List<MeetingInfo>> GetRecentAsync()
    {
        var all = new List<MeetingInfo>();
        foreach (var folder in new[] { FolderType.Archive, FolderType.Incoming, FolderType.Processing })
        {
            all.AddRange(await GetFromFolderAsync(FolderPath(folder)!, folder));
        }

        return all.OrderByDescending(meeting => meeting.LastModified).Take(RecentCount).ToList();
    }

    private async Task<List<MeetingInfo>> GetFavoritesAsync(string? favoriteFileNames)
    {
        var favorites = new List<MeetingInfo>();
        foreach (var fileName in (favoriteFileNames ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Find(fileName) is { } found)
            {
                favorites.Add(await DescribeAsync(found.Path, found.Folder));
            }
        }

        return favorites;
    }

    /// <summary>
    /// A meeting as the lists show it; an archived name (yyyyMMdd_HHmmss_status_Language_name) gives
    /// the processing date, the status and the language
    /// </summary>
    private async Task<MeetingInfo> DescribeAsync(string filePath, FolderType folderType)
    {
        var file = new FileInfo(filePath);
        var parts = Path.GetFileNameWithoutExtension(file.Name).Split('_');
        var meeting = new MeetingInfo
        {
            FileName = file.Name,
            OriginalName = ArchiveNames.BaseName(file.Name) + file.Extension,
            Size = file.Length,
            LastModified = file.LastWriteTime,
            FolderType = folderType,
            Status = "unknown",
            Language = "unknown",
            ProcessingDate = file.CreationTime,
            Date = file.LastWriteTime
        };

        if (folderType == FolderType.Archive && parts.Length >= 4)
        {
            meeting.Status = parts[2];
            if (ArchiveNames.IsLanguageName(parts[3]))
            {
                meeting.Language = parts[3];
            }

            if (DateTime.TryParseExact($"{parts[0]}_{parts[1]}", "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var processed))
            {
                meeting.ProcessingDate = processed;
                meeting.Date = processed;
            }
        }

        try
        {
            var metadata = await LoadMetadataAsync(file.Name);
            var transcript = metadata ?? await _reader.ReadAsync(filePath);

            meeting.Title = transcript.Title.Length > 100 ? transcript.Title[..100] + "..." : transcript.Title;
            meeting.PreviewContent = transcript.Content.Length > 200 ? transcript.Content[..200] + "..." : transcript.Content;
            meeting.Participants = transcript.Participants;
            meeting.HasJiraTickets = metadata?.CreatedJiraTickets.Count > 0;
            meeting.ActionItemCount = metadata?.ActionItems.Count ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read {FileName}", file.Name);
            meeting.Title = file.Name;
            meeting.PreviewContent = "Unable to preview content";
        }

        return meeting;
    }

    private static List<MeetingInfo> Filter(List<MeetingInfo> meetings, MeetingQuery query)
    {
        static List<string> Values(string? commaSeparated) =>
            (commaSeparated ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.ToLowerInvariant())
                .ToList();

        IEnumerable<MeetingInfo> filtered = meetings;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            filtered = filtered.Where(m =>
                m.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || m.OriginalName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || m.PreviewContent.Contains(search, StringComparison.OrdinalIgnoreCase)
                || m.Participants.Any(p => p.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        var statuses = Values(query.Status);
        if (statuses.Count > 0)
            filtered = filtered.Where(m => statuses.Contains(m.Status.ToLowerInvariant()));

        var languages = Values(query.Language);
        if (languages.Count > 0)
            filtered = filtered.Where(m => languages.Contains(m.Language.ToLowerInvariant()));

        var participants = Values(query.Participants);
        if (participants.Count > 0)
            filtered = filtered.Where(m => participants.Any(p => m.Participants.Any(mp => mp.Contains(p, StringComparison.OrdinalIgnoreCase))));

        if (query.DateFrom.HasValue)
            filtered = filtered.Where(m => m.Date >= query.DateFrom.Value);
        if (query.DateTo.HasValue)
            filtered = filtered.Where(m => m.Date <= query.DateTo.Value);
        if (query.HasJiraTickets.HasValue)
            filtered = filtered.Where(m => m.HasJiraTickets == query.HasJiraTickets.Value);

        return filtered.ToList();
    }

    private static List<MeetingInfo> Sort(List<MeetingInfo> meetings, string sortBy, string sortOrder)
    {
        Func<MeetingInfo, object> key = sortBy.ToLowerInvariant() switch
        {
            "title" => m => m.Title,
            "size" => m => m.Size,
            "status" => m => m.Status,
            "language" => m => m.Language,
            "participants" => m => m.Participants.Count,
            _ => m => m.Date
        };

        return string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase)
            ? meetings.OrderByDescending(key).ToList()
            : meetings.OrderBy(key).ToList();
    }
}
