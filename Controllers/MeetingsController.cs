using Microsoft.AspNetCore.Mvc;
using MeetingTranscriptProcessor.Models;
using MeetingTranscriptProcessor.Services;
using System.Text.Json;

namespace MeetingTranscriptProcessor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeetingsController : ControllerBase
    {
        private readonly ITranscriptProcessorService _transcriptProcessor;
        private readonly IConfigurationService _configurationService;
        private readonly string _archivePath;
        private readonly string _incomingPath;
        private readonly string _processingPath;

        public MeetingsController(
            ITranscriptProcessorService transcriptProcessor,
            IConfigurationService configurationService
        )
        {
            _transcriptProcessor = transcriptProcessor;
            _configurationService = configurationService;

            // Get paths from environment or use defaults
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var projectRoot =
                Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName ?? baseDir;

            _archivePath =
                Environment.GetEnvironmentVariable("ARCHIVE_DIRECTORY")
                ?? Path.Combine(projectRoot, "Data", "Archive");
            _incomingPath =
                Environment.GetEnvironmentVariable("INCOMING_DIRECTORY")
                ?? Path.Combine(projectRoot, "Data", "Incoming");
            _processingPath =
                Environment.GetEnvironmentVariable("PROCESSING_DIRECTORY")
                ?? Path.Combine(projectRoot, "Data", "Processing");
        }

        [HttpGet("folders")]
        public async Task<ActionResult<List<FolderInfo>>> GetFolders()
        {
            try
            {
                var folders = new List<FolderInfo>
                {
                    new FolderInfo
                    {
                        Name = "Archive",
                        Path = _archivePath,
                        Type = FolderType.Archive,
                        MeetingCount = await GetMeetingCountInFolder(_archivePath)
                    },
                    new FolderInfo
                    {
                        Name = "Incoming",
                        Path = _incomingPath,
                        Type = FolderType.Incoming,
                        MeetingCount = await GetMeetingCountInFolder(_incomingPath)
                    },
                    new FolderInfo
                    {
                        Name = "Processing",
                        Path = _processingPath,
                        Type = FolderType.Processing,
                        MeetingCount = await GetMeetingCountInFolder(_processingPath)
                    },
                    new FolderInfo
                    {
                        Name = "Recent",
                        Path = "", // Recent is virtual, no physical path
                        Type = FolderType.Recent,
                        MeetingCount = await GetRecentMeetingCount()
                    },
                    new FolderInfo
                    {
                        Name = "Favorites",
                        Path = "", // Favorites is virtual, no physical path
                        Type = FolderType.Favorites,
                        MeetingCount = await GetFavoriteMeetingCount()
                    }
                };

                return Ok(folders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("folders/{folderType}/meetings")]
        public async Task<ActionResult<List<MeetingInfo>>> GetMeetingsInFolder(
            FolderType folderType,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? language = null,
            [FromQuery] string? participants = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] bool? hasJiraTickets = null,
            [FromQuery] string sortBy = "date",
            [FromQuery] string sortOrder = "desc",
            [FromQuery] string? favoriteFileNames = null
        )
        {
            try
            {
                List<MeetingInfo> meetings;

                if (folderType == FolderType.Recent)
                {
                    meetings = await GetRecentMeetings();
                }
                else if (folderType == FolderType.Favorites)
                {
                    meetings = await GetFavoriteMeetings(favoriteFileNames);
                }
                else
                {
                    var folderPath = folderType switch
                    {
                        FolderType.Archive => _archivePath,
                        FolderType.Incoming => _incomingPath,
                        FolderType.Processing => _processingPath,
                        _ => throw new ArgumentException("Invalid folder type")
                    };

                    meetings = await GetMeetingsFromFolder(folderPath, folderType);
                }

                // Apply filters
                meetings = ApplyFilters(
                    meetings,
                    search,
                    status,
                    language,
                    participants,
                    dateFrom,
                    dateTo,
                    hasJiraTickets
                );

                // Apply sorting
                meetings = ApplySorting(meetings, sortBy, sortOrder);

                return Ok(meetings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("meeting/{fileName}")]
        public async Task<ActionResult<MeetingTranscript>> GetMeeting(string fileName)
        {
            try
            {
                // Search in all directories
                var allPaths = new[] { _archivePath, _incomingPath, _processingPath };

                foreach (var path in allPaths)
                {
                    var filePath = Path.Combine(path, fileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        var transcript = await _transcriptProcessor.ProcessTranscriptAsync(
                            filePath
                        );
                        return Ok(transcript);
                    }
                }

                return NotFound(new { error = "Meeting file not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("upload")]
        public async Task<ActionResult> UploadMeeting(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "No file provided" });

                var allowedExtensions = new[] { ".txt", ".md", ".json", ".xml", ".docx", ".pdf" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                    return BadRequest(new { error = "File type not supported" });

                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{file.FileName}";
                var filePath = Path.Combine(_incomingPath, fileName);

                Directory.CreateDirectory(_incomingPath);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return Ok(new { message = "File uploaded successfully", fileName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("meeting/{fileName}")]
        public async Task<ActionResult> DeleteMeeting(string fileName)
        {
            try
            {
                var allPaths = new[] { _archivePath, _incomingPath, _processingPath };

                foreach (var path in allPaths)
                {
                    var filePath = Path.Combine(path, fileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                        return Ok(new { message = "Meeting deleted successfully" });
                    }
                }

                return NotFound(new { error = "Meeting file not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("meeting/{fileName}/title")]
        public async Task<ActionResult> UpdateMeetingTitle(string fileName, [FromBody] UpdateMeetingTitleDto request)
        {
            try
            {
                // Validate model state
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new { error = "Validation failed", details = errors });
                }

                // Additional server-side validation
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return BadRequest(new { error = "Title cannot be empty" });
                }

                // Trim and sanitize the title
                var sanitizedTitle = request.Title.Trim();
                if (sanitizedTitle.Length > 200)
                {
                    sanitizedTitle = sanitizedTitle.Substring(0, 200);
                }

                var allPaths = new[] { _archivePath, _incomingPath, _processingPath };

                foreach (var path in allPaths)
                {
                    var filePath = Path.Combine(path, fileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        // Read the current file content
                        var content = await System.IO.File.ReadAllTextAsync(filePath);

                        // Update the title in the content
                        // For most text files, we'll prepend/update the first line as the title
                        var lines = content.Split('\n');

                        // If the file has content, replace the first line with the new title
                        if (lines.Length > 0)
                        {
                            // Check if the first line looks like a title (not too long, not starting with specific patterns)
                            var firstLine = lines[0].Trim();
                            bool firstLineIsLikelyTitle = !string.IsNullOrEmpty(firstLine) &&
                                                        firstLine.Length <= 200 &&
                                                        !firstLine.StartsWith("Date:") &&
                                                        !firstLine.StartsWith("Time:") &&
                                                        !firstLine.StartsWith("Participants:") &&
                                                        !firstLine.StartsWith("Meeting ID:");

                            if (firstLineIsLikelyTitle)
                            {
                                lines[0] = sanitizedTitle;
                            }
                            else
                            {
                                // Prepend the title as the first line
                                var newLines = new string[lines.Length + 1];
                                newLines[0] = sanitizedTitle;
                                Array.Copy(lines, 0, newLines, 1, lines.Length);
                                lines = newLines;
                            }
                        }
                        else
                        {
                            // Empty file, just add the title
                            lines = new[] { sanitizedTitle };
                        }

                        // Write the updated content back to the file
                        var updatedContent = string.Join('\n', lines);
                        await System.IO.File.WriteAllTextAsync(filePath, updatedContent);

                        return Ok(new { message = "Meeting title updated successfully", title = sanitizedTitle });
                    }
                }

                return NotFound(new { error = "Meeting file not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("meeting/{fileName}/move")]
        public ActionResult MoveMeeting(string fileName, [FromBody] MoveMeetingDto request)
        {
            try
            {
                // Validate model state
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new { error = "Validation failed", details = errors });
                }

                // Get source and target folder paths
                var sourcePaths = new[] { _archivePath, _incomingPath, _processingPath };
                var targetPath = GetFolderPath(request.TargetFolderType);

                if (string.IsNullOrEmpty(targetPath))
                {
                    return BadRequest(new { error = "Invalid target folder type" });
                }

                // Validate target folder type (Recent and Favorites are virtual, Processing is typically system-managed)
                if (request.TargetFolderType == FolderType.Recent)
                {
                    return BadRequest(new { error = "Cannot move meetings to Recent folder (it's virtual)" });
                }

                if (request.TargetFolderType == FolderType.Favorites)
                {
                    return BadRequest(new { error = "Cannot move meetings to Favorites folder (it's virtual)" });
                }

                if (request.TargetFolderType == FolderType.Processing)
                {
                    return BadRequest(new { error = "Cannot move meetings to Processing folder (it's system-managed)" });
                }

                // Ensure target directory exists
                Directory.CreateDirectory(targetPath);

                string? sourceFilePath = null;
                string? sourceFolderName = null;

                // Find the source file
                foreach (var path in sourcePaths)
                {
                    var filePath = Path.Combine(path, fileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        sourceFilePath = filePath;
                        sourceFolderName = Path.GetFileName(path);
                        break;
                    }
                }

                if (sourceFilePath == null)
                {
                    return NotFound(new { error = "Meeting file not found" });
                }

                // Check if the file is already in the target folder
                if (sourceFilePath.StartsWith(targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { error = "Meeting is already in the target folder" });
                }

                // Create target file path
                var targetFilePath = Path.Combine(targetPath, fileName);

                // Handle duplicate names in target folder
                var counter = 1;
                while (System.IO.File.Exists(targetFilePath))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var extension = Path.GetExtension(fileName);
                    var newFileName = $"{nameWithoutExt}_{counter}{extension}";
                    targetFilePath = Path.Combine(targetPath, newFileName);
                    fileName = newFileName; // Update fileName for response
                    counter++;
                }

                // Move the file
                System.IO.File.Move(sourceFilePath, targetFilePath);

                var targetFolderName = Path.GetFileName(targetPath);
                return Ok(new
                {
                    message = $"Meeting moved successfully from {sourceFolderName} to {targetFolderName}",
                    fileName = fileName,
                    targetFolder = request.TargetFolderType
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<int> GetMeetingCountInFolder(string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                    return 0;

                var supportedExtensions = new[] { ".txt", ".md", ".json", ".xml", ".docx", ".pdf" };
                var files = Directory
                    .GetFiles(folderPath)
                    .Where(
                        f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())
                    )
                    .ToList();

                return await Task.FromResult(files.Count);
            }
            catch
            {
                return 0;
            }
        }

        private async Task<List<MeetingInfo>> GetMeetingsFromFolder(
            string folderPath,
            FolderType folderType
        )
        {
            var meetings = new List<MeetingInfo>();

            try
            {
                if (!Directory.Exists(folderPath))
                    return meetings;

                var supportedExtensions = new[] { ".txt", ".md", ".json", ".xml", ".docx", ".pdf" };
                var files = Directory
                    .GetFiles(folderPath)
                    .Where(
                        f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())
                    )
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToList();

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var fileName = Path.GetFileName(file);

                    // Parse archive filename format: yyyyMMdd_HHmmss_status_language_originalname
                    var parts = fileName.Split('_');
                    var meetingInfo = new MeetingInfo
                    {
                        FileName = fileName,
                        OriginalName =
                            folderType == FolderType.Archive && parts.Length >= 4
                                ? string.Join("_", parts.Skip(3))
                                : fileName,
                        Size = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime,
                        FolderType = folderType,
                        Status =
                            folderType == FolderType.Archive && parts.Length >= 3
                                ? parts[2]
                                : "unknown",
                        Language =
                            folderType == FolderType.Archive && parts.Length >= 4
                                ? parts[3].Split('.')[0] // Remove file extension if present
                                : "unknown",
                        ProcessingDate = fileInfo.CreationTime,
                        Date = fileInfo.LastWriteTime
                    };

                    // Parse processing date from filename if available
                    if (folderType == FolderType.Archive && parts.Length >= 2)
                    {
                        if (
                            DateTime.TryParseExact(
                                $"{parts[0]}_{parts[1]}",
                                "yyyyMMdd_HHmmss",
                                null,
                                System.Globalization.DateTimeStyles.None,
                                out var parsedDate
                            )
                        )
                        {
                            meetingInfo.ProcessingDate = parsedDate;
                            meetingInfo.Date = parsedDate;
                        }
                    }

                    // Try to extract basic info without full processing
                    try
                    {
                        var content = await System.IO.File.ReadAllTextAsync(file);
                        meetingInfo.PreviewContent =
                            content.Length > 200 ? content.Substring(0, 200) + "..." : content;

                        // Try to detect meeting title from first few lines
                        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        meetingInfo.Title = lines.FirstOrDefault()?.Trim() ?? "Untitled Meeting";

                        if (meetingInfo.Title.Length > 100)
                            meetingInfo.Title = meetingInfo.Title.Substring(0, 100) + "...";

                        // Extract participants from content (basic pattern matching)
                        meetingInfo.Participants = ExtractParticipants(content);

                        // Check for Jira tickets
                        meetingInfo.HasJiraTickets =
                            content.Contains("JIRA")
                            || content.Contains("Jira")
                            || content.Contains("jira")
                            || content.Contains("ticket");

                        // Count action items
                        meetingInfo.ActionItemCount = CountActionItems(content);
                    }
                    catch
                    {
                        meetingInfo.PreviewContent = "Unable to preview content";
                        meetingInfo.Title = fileName;
                    }

                    meetings.Add(meetingInfo);
                }
            }
            catch (Exception ex)
            {
                // Log error but return partial results
                Console.WriteLine($"Error reading folder {folderPath}: {ex.Message}");
            }

            return meetings;
        }

        private async Task<List<MeetingInfo>> GetRecentMeetings()
        {
            var allMeetings = new List<MeetingInfo>();

            // Get meetings from all folders
            var archiveMeetings = await GetMeetingsFromFolder(_archivePath, FolderType.Archive);
            var incomingMeetings = await GetMeetingsFromFolder(_incomingPath, FolderType.Incoming);
            var processingMeetings = await GetMeetingsFromFolder(
                _processingPath,
                FolderType.Processing
            );

            allMeetings.AddRange(archiveMeetings);
            allMeetings.AddRange(incomingMeetings);
            allMeetings.AddRange(processingMeetings);

            // Sort by LastModified date (most recent first) and take top 5
            return allMeetings.OrderByDescending(m => m.LastModified).Take(5).ToList();
        }

        private async Task<int> GetRecentMeetingCount()
        {
            // Recent folder always shows max 5 meetings
            var recentMeetings = await GetRecentMeetings();
            return recentMeetings.Count;
        }

        private async Task<int> GetFavoriteMeetingCount()
        {
            // Since favorites are stored client-side in localStorage, 
            // we'll return 0 here. The actual count will be managed by the frontend.
            // In a real application, you might store favorites in a database.
            return await Task.FromResult(0);
        }

        private async Task<List<MeetingInfo>> GetFavoriteMeetings(string? favoriteFileNames)
        {
            var favoriteMeetings = new List<MeetingInfo>();

            if (string.IsNullOrWhiteSpace(favoriteFileNames))
            {
                return favoriteMeetings;
            }

            // Parse comma-separated favorite file names
            var fileNames = favoriteFileNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(name => name.Trim())
                                           .Where(name => !string.IsNullOrEmpty(name))
                                           .ToList();

            if (!fileNames.Any())
            {
                return favoriteMeetings;
            }

            // Search for favorite files across all directories
            var allPaths = new[] { _archivePath, _incomingPath, _processingPath };

            foreach (var fileName in fileNames)
            {
                foreach (var path in allPaths)
                {
                    var filePath = Path.Combine(path, fileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        var folderType = path == _archivePath ? FolderType.Archive :
                                        path == _incomingPath ? FolderType.Incoming :
                                        FolderType.Processing;

                        var meetings = await GetMeetingsFromFolder(path, folderType);
                        var meeting = meetings.FirstOrDefault(m => m.FileName == fileName);

                        if (meeting != null)
                        {
                            favoriteMeetings.Add(meeting);
                            break; // Found the file, no need to search in other directories
                        }
                    }
                }
            }

            return favoriteMeetings;
        }

        private List<string> ExtractParticipants(string content)
        {
            var participants = new List<string>();
            var lines = content.Split('\n');

            // Look for patterns like "Participants:", "Attendees:", etc.
            foreach (var line in lines)
            {
                var lowerLine = line.ToLower().Trim();
                if (
                    lowerLine.StartsWith("participants:")
                    || lowerLine.StartsWith("attendees:")
                    || lowerLine.StartsWith("present:")
                    || lowerLine.StartsWith("meeting participants:")
                )
                {
                    var participantsPart = line.Substring(line.IndexOf(':') + 1).Trim();
                    var names = participantsPart
                        .Split(new[] { ',', ';', '&' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToList();
                    participants.AddRange(names);
                    break;
                }
            }

            return participants.Distinct().ToList();
        }

        private int CountActionItems(string content)
        {
            var actionItemPatterns = new[]
            {
                "action item",
                "action:",
                "todo:",
                "follow up",
                "next step"
            };
            var lines = content.Split('\n');
            int count = 0;

            foreach (var line in lines)
            {
                var lowerLine = line.ToLower();
                if (actionItemPatterns.Any(pattern => lowerLine.Contains(pattern)))
                {
                    count++;
                }
            }

            return count;
        }

        private List<MeetingInfo> ApplyFilters(
            List<MeetingInfo> meetings,
            string? search,
            string? status,
            string? language,
            string? participants,
            DateTime? dateFrom,
            DateTime? dateTo,
            bool? hasJiraTickets
        )
        {
            var filtered = meetings.AsEnumerable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                filtered = filtered.Where(
                    m =>
                        m.Title.ToLower().Contains(searchLower)
                        || m.OriginalName.ToLower().Contains(searchLower)
                        || m.PreviewContent.ToLower().Contains(searchLower)
                        || m.Participants.Any(p => p.ToLower().Contains(searchLower))
                );
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                var statusList = status.Split(',').Select(s => s.Trim().ToLower()).ToList();
                filtered = filtered.Where(m => statusList.Contains(m.Status.ToLower()));
            }

            // Language filter
            if (!string.IsNullOrWhiteSpace(language))
            {
                var languageList = language.Split(',').Select(l => l.Trim().ToLower()).ToList();
                filtered = filtered.Where(m => languageList.Contains(m.Language.ToLower()));
            }

            // Participants filter
            if (!string.IsNullOrWhiteSpace(participants))
            {
                var participantList = participants
                    .Split(',')
                    .Select(p => p.Trim().ToLower())
                    .ToList();
                filtered = filtered.Where(
                    m =>
                        participantList.Any(p => m.Participants.Any(mp => mp.ToLower().Contains(p)))
                );
            }

            // Date range filter
            if (dateFrom.HasValue)
            {
                filtered = filtered.Where(m => m.Date >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                filtered = filtered.Where(m => m.Date <= dateTo.Value);
            }

            // Jira tickets filter
            if (hasJiraTickets.HasValue)
            {
                filtered = filtered.Where(m => m.HasJiraTickets == hasJiraTickets.Value);
            }

            return filtered.ToList();
        }

        private List<MeetingInfo> ApplySorting(
            List<MeetingInfo> meetings,
            string sortBy,
            string sortOrder
        )
        {
            var isDescending = sortOrder.ToLower() == "desc";

            return sortBy.ToLower() switch
            {
                "title"
                    => isDescending
                        ? meetings.OrderByDescending(m => m.Title).ToList()
                        : meetings.OrderBy(m => m.Title).ToList(),
                "size"
                    => isDescending
                        ? meetings.OrderByDescending(m => m.Size).ToList()
                        : meetings.OrderBy(m => m.Size).ToList(),
                "status"
                    => isDescending
                        ? meetings.OrderByDescending(m => m.Status).ToList()
                        : meetings.OrderBy(m => m.Status).ToList(),
                "language"
                    => isDescending
                        ? meetings.OrderByDescending(m => m.Language).ToList()
                        : meetings.OrderBy(m => m.Language).ToList(),
                "participants"
                    => isDescending
                        ? meetings.OrderByDescending(m => m.Participants.Count).ToList()
                        : meetings.OrderBy(m => m.Participants.Count).ToList(),
                "date"
                or _
                    => isDescending
                        ? meetings.OrderByDescending(m => m.Date).ToList()
                        : meetings.OrderBy(m => m.Date).ToList()
            };
        }

        /// <summary>
        /// Gets the folder path for a given folder type
        /// </summary>
        private string? GetFolderPath(FolderType folderType)
        {
            return folderType switch
            {
                FolderType.Archive => _archivePath,
                FolderType.Incoming => _incomingPath,
                FolderType.Processing => _processingPath,
                _ => null
            };
        }
    }
}
