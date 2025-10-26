using Microsoft.AspNetCore.Mvc;
using MeetingTranscriptProcessor.Models;
using MeetingTranscriptProcessor.Services;
using System.Text.Json;
using Newtonsoft.Json;

namespace MeetingTranscriptProcessor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeetingsController : ControllerBase
    {
        private readonly ITranscriptProcessorService _transcriptProcessor;
        private readonly IConfigurationService _configurationService;
        private readonly IJiraTicketService _jiraTicketService;
        private readonly IProcessingStatusService _processingStatusService;
        private readonly string _archivePath;
        private readonly string _incomingPath;
        private readonly string _processingPath;

        public MeetingsController(
            ITranscriptProcessorService transcriptProcessor,
            IConfigurationService configurationService,
            IJiraTicketService jiraTicketService,
            IProcessingStatusService processingStatusService
        )
        {
            _transcriptProcessor = transcriptProcessor;
            _configurationService = configurationService;
            _jiraTicketService = jiraTicketService;
            _processingStatusService = processingStatusService;

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
                Console.WriteLine($"🎯 GetMeeting called for fileName: {fileName}");

                // Try to load from metadata file first (has JIRA ticket references)
                var transcript = await LoadTranscriptWithMetadata(fileName);
                if (transcript != null)
                {
                    Console.WriteLine($"✅ Loaded transcript from metadata successfully");
                    Console.WriteLine($"📊 Returning transcript with {transcript.CreatedJiraTickets?.Count ?? 0} JIRA tickets");
                    return Ok(transcript);
                }

                Console.WriteLine($"⚠️ No metadata found, falling back to processing original file");

                // Fallback to processing the original file
                var allPaths = new[] { _archivePath, _incomingPath, _processingPath };

                foreach (var path in allPaths)
                {
                    var filePath = Path.Combine(path, fileName);
                    Console.WriteLine($"🔍 Checking fallback path: {filePath}");
                    if (System.IO.File.Exists(filePath))
                    {
                        Console.WriteLine($"📄 Found file, processing transcript...");
                        transcript = await _transcriptProcessor.ProcessTranscriptAsync(filePath);
                        Console.WriteLine($"✅ Processed transcript, returning result");
                        return Ok(transcript);
                    }
                }

                Console.WriteLine($"❌ Meeting file not found in any directory");
                return NotFound(new { error = "Meeting file not found" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetMeeting: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("meeting/{fileName}/tickets")]
        public async Task<ActionResult<List<JiraTicketReference>>> GetMeetingJiraTickets(string fileName)
        {
            try
            {
                Console.WriteLine($"🎫 GetMeetingJiraTickets called for fileName: {fileName}");

                // Try to load from metadata file first
                var transcript = await LoadTranscriptWithMetadata(fileName);
                if (transcript != null)
                {
                    Console.WriteLine($"✅ Loaded transcript from metadata for tickets");
                    Console.WriteLine($"🎫 Found {transcript.CreatedJiraTickets?.Count ?? 0} JIRA tickets");

                    if (transcript.CreatedJiraTickets?.Count > 0)
                    {
                        foreach (var ticket in transcript.CreatedJiraTickets)
                        {
                            Console.WriteLine($"   - Ticket: {ticket.TicketKey} | {ticket.Title}");
                        }
                    }

                    return Ok(transcript.CreatedJiraTickets);
                }

                Console.WriteLine($"❌ No metadata found for JIRA tickets, returning empty list");
                // Fallback: return empty list if no metadata found
                return Ok(new List<JiraTicketReference>());
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

                var allowedExtensions = new[] { ".txt", ".md", ".json", ".xml", ".docx", ".pdf", ".vtt" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                    return BadRequest(new { error = "File type not supported" });

                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{file.FileName}";
                var filePath = Path.Combine(_incomingPath, fileName);

                Directory.CreateDirectory(_incomingPath);

                // Save the uploaded file to Incoming folder
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                Console.WriteLine($"� File uploaded: {fileName}");
                Console.WriteLine($"⏳ File will be processed automatically by background service");

                // Return immediately - let FileWatcherService process the file
                return Ok(new
                {
                    message = "File uploaded successfully and queued for processing",
                    fileName,
                    status = "uploaded"
                });
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

                var supportedExtensions = new[] { ".txt", ".md", ".json", ".xml", ".docx", ".pdf", ".vtt" };
                var files = Directory
                    .GetFiles(folderPath)
                    .Where(
                        f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())
                    )
                    .Where(f => !Path.GetFileName(f).EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase)) // Exclude metadata files
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

                var supportedExtensions = new[] { ".txt", ".md", ".json", ".xml", ".docx", ".pdf", ".vtt" };
                var files = Directory
                    .GetFiles(folderPath)
                    .Where(
                        f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())
                    )
                    .Where(f => !Path.GetFileName(f).EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase)) // Exclude metadata files
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
                        var content = await ReadFileContentForPreview(file);
                        meetingInfo.PreviewContent =
                            content.Length > 200 ? content.Substring(0, 200) + "..." : content;

                        // Try to get title from metadata file first (for smart titles), then fallback to file content
                        meetingInfo.Title = await GetTitleFromMetadataOrContent(fileName, content);

                        Console.WriteLine($"🔍 DEBUG GetMeetingsFromFolder - File: {fileName}");
                        Console.WriteLine($"   📋 Title extracted: '{meetingInfo.Title}'");
                        Console.WriteLine($"   📄 PreviewContent length: {meetingInfo.PreviewContent?.Length ?? 0}");

                        if (meetingInfo.Title.Length > 100)
                            meetingInfo.Title = meetingInfo.Title.Substring(0, 100) + "...";

                        // Try to load from metadata file for participants and JIRA tickets
                        var transcript = await LoadTranscriptWithMetadata(fileName);
                        if (transcript != null)
                        {
                            Console.WriteLine($"   ✅ Loaded metadata for {fileName}");
                            meetingInfo.Participants = transcript.Participants ?? new List<string>();
                            var jiraTickets = transcript.CreatedJiraTickets?.Select(t => t.TicketKey).ToList() ?? new List<string>();
                            meetingInfo.HasJiraTickets = jiraTickets.Count > 0;
                            meetingInfo.ActionItemCount = transcript.ActionItems?.Count ?? 0;

                            Console.WriteLine($"   👥 Participants from metadata: {meetingInfo.Participants.Count}");
                            Console.WriteLine($"   🎫 JIRA tickets from metadata: {jiraTickets.Count}");
                            if (jiraTickets.Count > 0)
                            {
                                Console.WriteLine($"   🎫 First ticket: {jiraTickets.First()}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"   ⚠️ No metadata found for {fileName}, using fallback extraction");
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

            // Look for patterns like "Participants:", "Attendees:", "Deelnemers:", etc. (multi-language support)
            foreach (var line in lines)
            {
                var lowerLine = line.ToLower().Trim();
                if (
                    lowerLine.StartsWith("participants:")
                    || lowerLine.StartsWith("attendees:")
                    || lowerLine.StartsWith("present:")
                    || lowerLine.StartsWith("meeting participants:")
                    || lowerLine.StartsWith("deelnemers:")      // Dutch
                    || lowerLine.StartsWith("aanwezigen:")      // Dutch (alternative)
                    || lowerLine.StartsWith("participants:")    // French (same as English)
                    || lowerLine.StartsWith("présents:")        // French
                )
                {
                    var participantsPart = line.Substring(line.IndexOf(':') + 1).Trim();

                    // Split by comma, semicolon, or ampersand
                    var rawNames = participantsPart
                        .Split(new[] { ',', ';', '&' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToList();

                    // Clean up participant names by removing role descriptions in parentheses
                    foreach (var name in rawNames)
                    {
                        // Remove role descriptions like "(Product Owner - Extern)" and extract just the name
                        var cleanName = System.Text.RegularExpressions.Regex.Replace(name, @"\s*\([^)]*\)", "").Trim();
                        if (!string.IsNullOrEmpty(cleanName))
                        {
                            participants.Add(cleanName);
                        }
                    }
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

        /// <summary>
        /// Saves transcript metadata including JIRA ticket references
        /// </summary>
        private async Task SaveTranscriptMetadataAsync(MeetingTranscript transcript, string archivedFilePath)
        {
            try
            {
                Console.WriteLine($"🔄 SaveTranscriptMetadataAsync called for: {Path.GetFileName(archivedFilePath)}");
                Console.WriteLine($"📊 Transcript has {transcript.CreatedJiraTickets.Count} JIRA tickets");

                // Extract base filename from archived file (removes timestamp prefix)
                var archivedFileName = Path.GetFileNameWithoutExtension(archivedFilePath);
                var baseFileName = ExtractBaseFileName(Path.GetFileName(archivedFilePath));
                var metadataFileName = $"{baseFileName}.meta.json";
                var metadataPath = Path.Combine(Path.GetDirectoryName(archivedFilePath) ?? "", metadataFileName);

                Console.WriteLine($"📂 Base filename: {baseFileName}");
                Console.WriteLine($"📂 Metadata path: {metadataPath}");
                Console.WriteLine($"📂 Directory exists: {Directory.Exists(Path.GetDirectoryName(metadataPath))}");

                // Ensure directory exists
                var directory = Path.GetDirectoryName(metadataPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Console.WriteLine($"📁 Created directory: {directory}");
                }

                // Serialize transcript to JSON with comprehensive options
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var jsonContent = System.Text.Json.JsonSerializer.Serialize(transcript, options);
                Console.WriteLine($"📄 JSON content length: {jsonContent.Length} characters");
                Console.WriteLine($"🎫 JIRA tickets in JSON: {transcript.CreatedJiraTickets.Count}");

                await System.IO.File.WriteAllTextAsync(metadataPath, jsonContent);

                // Verify file was created
                if (System.IO.File.Exists(metadataPath))
                {
                    var fileInfo = new FileInfo(metadataPath);
                    Console.WriteLine($"✅ Successfully saved metadata file: {metadataFileName} ({fileInfo.Length} bytes)");
                }
                else
                {
                    Console.WriteLine($"❌ Metadata file was not created: {metadataPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: Failed to save transcript metadata: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");

                // Re-throw to ensure the error is visible in processing
                throw new InvalidOperationException($"Failed to save metadata for {Path.GetFileName(archivedFilePath)}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Archives processed file to archive directory with timestamp
        /// </summary>
        private string ArchiveFile(string filePath, string status, string? languageCode = null)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                    return "";

                var fileName = Path.GetFileName(filePath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                // Include language in filename if available
                var languageInfo = string.IsNullOrEmpty(languageCode) ? "" : $"_{GetLanguageName(languageCode)}";
                var archivedFileName = $"{timestamp}_{status}{languageInfo}_{fileName}";
                var archivedPath = Path.Combine(_archivePath, archivedFileName);

                // Move file to archive
                System.IO.File.Move(filePath, archivedPath);

                // Also move metadata file if it exists (this is legacy - new approach creates metadata after archiving)
                var originalFileName = Path.GetFileNameWithoutExtension(filePath);
                var metadataFileName = $"{originalFileName}.meta.json";
                var metadataFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? "", metadataFileName);

                if (System.IO.File.Exists(metadataFilePath))
                {
                    // Archive metadata with base filename only (no timestamp prefix)
                    // This allows LoadTranscriptWithMetadata to find it using the extracted base filename
                    var baseFileName = ExtractBaseFileName(fileName);
                    var archivedMetadataFileName = $"{baseFileName}.meta.json";
                    var archivedMetadataPath = Path.Combine(_archivePath, archivedMetadataFileName);
                    System.IO.File.Move(metadataFilePath, archivedMetadataPath);
                    Console.WriteLine($"📦 Archived metadata: {archivedMetadataFileName}");
                }

                Console.WriteLine($"📦 Archived: {archivedFileName}");
                return archivedPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Warning: Failed to archive file {Path.GetFileName(filePath)}: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Gets language name from language code
        /// </summary>
        private static string GetLanguageName(string? languageCode)
        {
            return languageCode?.ToLowerInvariant() switch
            {
                "en" => "English",
                "fr" => "French",
                "nl" => "Dutch",
                "de" => "German",
                "es" => "Spanish",
                _ => languageCode ?? "Unknown"
            };
        }

        /// <summary>
        /// Loads transcript with metadata (including JIRA ticket references) if available
        /// </summary>
        private async Task<MeetingTranscript?> LoadTranscriptWithMetadata(string fileName)
        {
            try
            {
                Console.WriteLine($"🔍 LoadTranscriptWithMetadata called for: {fileName}");

                // Remove timestamp prefix to get base filename
                var baseFileName = ExtractBaseFileName(fileName);
                var metadataFileName = $"{baseFileName}.meta.json";

                Console.WriteLine($"🔍 Base filename extracted: {baseFileName}");
                Console.WriteLine($"🔍 Looking for metadata file: {metadataFileName}");

                // Search in all directories for metadata file
                var allPaths = new[] { _archivePath, _incomingPath, _processingPath };

                // First, let's list all files in the archive directory for debugging
                Console.WriteLine($"📁 Listing all files in archive directory ({_archivePath}):");
                try
                {
                    var archiveFiles = Directory.GetFiles(_archivePath);
                    foreach (var file in archiveFiles)
                    {
                        var archiveFileName = Path.GetFileName(file);
                        Console.WriteLine($"   📄 Archive file: {archiveFileName}");
                        if (archiveFileName.EndsWith(".meta.json"))
                        {
                            Console.WriteLine($"      🎯 Found metadata file: {archiveFileName}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Error listing archive files: {ex.Message}");
                }

                foreach (var path in allPaths)
                {
                    var metadataPath = Path.Combine(path, metadataFileName);
                    Console.WriteLine($"🔍 Checking path: {metadataPath}");
                    Console.WriteLine($"🔍 File exists: {System.IO.File.Exists(metadataPath)}");

                    if (System.IO.File.Exists(metadataPath))
                    {
                        Console.WriteLine($"✅ Found metadata file at: {metadataPath}");
                        var jsonContent = await System.IO.File.ReadAllTextAsync(metadataPath);
                        Console.WriteLine($"📄 Metadata file size: {jsonContent.Length} characters");

                        var options = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };
                        var transcript = System.Text.Json.JsonSerializer.Deserialize<MeetingTranscript>(jsonContent, options);

                        if (transcript != null)
                        {
                            Console.WriteLine($"✅ Successfully deserialized transcript");
                            Console.WriteLine($"📊 Transcript data:");
                            Console.WriteLine($"   - ID: {transcript.Id}");
                            Console.WriteLine($"   - Title: {transcript.Title}");
                            Console.WriteLine($"   - Action items count: {transcript.ActionItems?.Count ?? 0}");
                            Console.WriteLine($"   - JIRA tickets count: {transcript.CreatedJiraTickets?.Count ?? 0}");

                            // Convert JSON content to readable text if it's in JSON format
                            if (!string.IsNullOrEmpty(transcript.Content) &&
                                transcript.Content.TrimStart().StartsWith("{"))
                            {
                                Console.WriteLine($"🔄 Converting JSON content to readable text...");
                                try
                                {
                                    var transcriptData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(transcript.Content);
                                    if (transcriptData?.combinedTranscript != null && transcriptData?.participants != null)
                                    {
                                        transcript.Content = ProcessMsTeamsTranscriptForDisplay(transcriptData);
                                        Console.WriteLine($"✅ Successfully converted JSON to readable text ({transcript.Content.Length} chars)");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"⚠️ Failed to convert JSON content: {ex.Message}");
                                }
                            }

                            if (transcript.CreatedJiraTickets?.Count > 0)
                            {
                                Console.WriteLine($"🎫 JIRA tickets found:");
                                foreach (var ticket in transcript.CreatedJiraTickets)
                                {
                                    Console.WriteLine($"   - {ticket.TicketKey}: {ticket.Title}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"❌ No JIRA tickets found in transcript!");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"❌ Failed to deserialize transcript from metadata");
                        }

                        return transcript;
                    }
                }

                Console.WriteLine($"❌ No metadata file found for base filename: {baseFileName}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error loading metadata for {fileName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts base filename by removing timestamp prefix
        /// <summary>
        /// Extracts the base filename from archived filename
        /// Handles patterns like: YYYYMMDD_HHMMSS_status_language_originalname
        /// Also handles complex cases like: YYYYMMDD_HHMMSS_status_language_YYYYMMDD_HHMMSS_originalname
        /// </summary>
        private static string ExtractBaseFileName(string fileName)
        {
            // Remove extension first
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            // Pattern: YYYYMMDD_HHMMSS_status_[language_]originalname
            // We want to extract the original name part
            var parts = nameWithoutExt.Split('_');

            Console.WriteLine($"🔍 ExtractBaseFileName input: {fileName}");
            Console.WriteLine($"🔍 nameWithoutExt: {nameWithoutExt}");
            Console.WriteLine($"🔍 parts: [{string.Join(", ", parts)}] (count: {parts.Length})");

            if (parts.Length >= 4)
            {
                // Check if this follows the archive pattern: YYYYMMDD_HHMMSS_status_[language_]originalname
                if (parts[0].Length == 8 && parts[0].All(char.IsDigit) && // YYYYMMDD
                    parts[1].Length == 6 && parts[1].All(char.IsDigit))   // HHMMSS
                {
                    Console.WriteLine($"🔍 Found archive pattern with timestamp prefix");

                    // Skip timestamp (parts 0,1) and status (part 2)
                    var skipCount = 3;

                    // Check if part 3 is a language name
                    if (parts.Length > 4 && IsLanguageName(parts[3]))
                    {
                        skipCount = 4; // also skip language
                        Console.WriteLine($"🔍 Found language part '{parts[3]}', skipCount = {skipCount}");
                    }
                    else
                    {
                        Console.WriteLine($"🔍 No language part found, skipCount = {skipCount}");
                    }

                    var remainingParts = parts.Skip(skipCount).ToList();
                    Console.WriteLine($"🔍 remainingParts after skip: [{string.Join(", ", remainingParts)}]");

                    if (remainingParts.Count > 0)
                    {
                        var result = string.Join("_", remainingParts);
                        Console.WriteLine($"🔍 ExtractBaseFileName result: {result}");
                        return result;
                    }
                }
            }

            // If pattern doesn't match, return as-is
            Console.WriteLine($"🔍 No archive pattern match, returning as-is: {nameWithoutExt}");
            return nameWithoutExt;
        }

        /// <summary>
        /// Checks if a string might be a language name
        /// </summary>
        private static bool IsLanguageName(string part)
        {
            var commonLanguages = new[] { "English", "French", "Dutch", "Spanish", "German" };
            return commonLanguages.Contains(part, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the title from metadata file if available, otherwise from file content
        /// </summary>
        private async Task<string> GetTitleFromMetadataOrContent(string fileName, string content)
        {
            try
            {
                // Try to get title from metadata file first
                var baseFileName = ExtractBaseFileName(fileName);
                var metadataFileName = $"{baseFileName}.meta.json";

                // Search in all directories for metadata file
                var allPaths = new[] { _archivePath, _incomingPath, _processingPath };

                foreach (var path in allPaths)
                {
                    var metadataPath = Path.Combine(path, metadataFileName);
                    if (System.IO.File.Exists(metadataPath))
                    {
                        var jsonContent = await System.IO.File.ReadAllTextAsync(metadataPath);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };
                        var transcript = System.Text.Json.JsonSerializer.Deserialize<MeetingTranscript>(jsonContent, options);
                        if (transcript != null && !string.IsNullOrWhiteSpace(transcript.Title))
                        {
                            return transcript.Title;
                        }
                    }
                }

                // Fallback to content-based title detection
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                return lines.FirstOrDefault()?.Trim() ?? "Untitled Meeting";
            }
            catch
            {
                // If anything fails, fallback to content-based title detection
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                return lines.FirstOrDefault()?.Trim() ?? "Untitled Meeting";
            }
        }

        /// <summary>
        /// Reads file content for preview, handling different file types including JSON conversion
        /// </summary>
        private static async Task<string> ReadFileContentForPreview(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                Console.WriteLine($"🔍 ReadFileContentForPreview called for: {filePath}");
                Console.WriteLine($"🔍 File extension: {extension}");

                if (extension == ".json")
                {
                    // Handle JSON files - check if it's MS Teams format and convert to readable text
                    var jsonContent = await System.IO.File.ReadAllTextAsync(filePath);
                    Console.WriteLine($"🔍 JSON content length: {jsonContent.Length}");

                    try
                    {
                        var transcriptData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonContent);

                        // Check for MS Teams/Graph API format
                        if (transcriptData?.combinedTranscript != null && transcriptData?.participants != null)
                        {
                            Console.WriteLine($"✅ Detected MS Teams format, processing for preview");
                            var convertedContent = ProcessMsTeamsTranscriptForPreview(transcriptData);
                            Console.WriteLine($"✅ Converted content length: {convertedContent.Length}");
                            return convertedContent;
                        }

                        Console.WriteLine($"⚠️ Not MS Teams format, checking other formats");
                        // Try common JSON transcript formats
                        if (transcriptData?.transcript != null)
                        {
                            Console.WriteLine($"🔍 Found .transcript field");
                            return transcriptData.transcript.ToString();
                        }

                        if (transcriptData?.content != null)
                        {
                            Console.WriteLine($"🔍 Found .content field");
                            return transcriptData.content.ToString();
                        }

                        if (transcriptData?.text != null)
                        {
                            Console.WriteLine($"🔍 Found .text field");
                            return transcriptData.text.ToString();
                        }

                        // If no standard format, return the JSON as text
                        Console.WriteLine($"⚠️ No recognized format, returning raw JSON");
                        return jsonContent;
                    }
                    catch
                    {
                        // If JSON parsing fails, treat as plain text
                        return jsonContent;
                    }
                }

                // For other file types, read as plain text
                return await System.IO.File.ReadAllTextAsync(filePath);
            }
            catch
            {
                return "Error reading file content";
            }
        }

        /// <summary>
        /// Simplified MS Teams transcript processor for preview purposes
        /// </summary>
        private static string ProcessMsTeamsTranscriptForPreview(dynamic transcriptData)
        {
            try
            {
                Console.WriteLine($"🔍 ProcessMsTeamsTranscriptForPreview called");
                var result = new List<string>();

                // Add header with meeting information
                result.Add("MEETING TRANSCRIPT");
                result.Add("=================");

                if (transcriptData.meetingTitle != null)
                {
                    result.Add($"Title: {transcriptData.meetingTitle}");
                    Console.WriteLine($"🔍 Added title: {transcriptData.meetingTitle}");
                }

                if (transcriptData.startTime != null)
                {
                    result.Add($"Date: {transcriptData.startTime}");
                    Console.WriteLine($"🔍 Added start time: {transcriptData.startTime}");
                }

                if (transcriptData.locale != null)
                {
                    result.Add($"Language: {transcriptData.locale}");
                }

                // Add participants
                if (transcriptData.participants != null)
                {
                    result.Add("Participants:");
                    foreach (var participant in transcriptData.participants)
                    {
                        if (participant.displayName != null)
                        {
                            result.Add($"- {participant.displayName}");
                        }
                    }
                }

                result.Add("");
                result.Add("--------------------------------------------------------------");
                result.Add("");

                // Convert first few transcript segments to readable format for preview
                if (transcriptData.combinedTranscript != null)
                {
                    int segmentCount = 0;
                    foreach (var segment in transcriptData.combinedTranscript)
                    {
                        // Skip system messages
                        if (segment.speaker?.id == "system")
                            continue;

                        var speakerName = segment.speaker?.displayName?.ToString() ?? "Unknown Speaker";
                        var text = segment.text?.ToString() ?? "";

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            result.Add($"{speakerName}: {text}");
                            result.Add("");
                            segmentCount++;

                            // Limit preview to first 5 segments
                            if (segmentCount >= 5) break;
                        }
                    }
                }

                return string.Join("\n", result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing MS Teams transcript for preview: {ex.Message}");
                // Fallback to raw JSON
                return Newtonsoft.Json.JsonConvert.SerializeObject(transcriptData, Newtonsoft.Json.Formatting.Indented);
            }
        }

        /// <summary>
        /// Full MS Teams transcript processor for complete meeting display
        /// </summary>
        private static string ProcessMsTeamsTranscriptForDisplay(dynamic transcriptData)
        {
            try
            {
                Console.WriteLine($"🔍 ProcessMsTeamsTranscriptForDisplay called");
                var result = new List<string>();

                // Add header with meeting information
                result.Add("MEETING TRANSCRIPT");
                result.Add("=================");
                result.Add("");

                if (transcriptData.meetingTitle != null)
                {
                    result.Add($"Title: {transcriptData.meetingTitle}");
                }

                if (transcriptData.startTime != null)
                {
                    result.Add($"Date: {transcriptData.startTime}");
                }

                if (transcriptData.endTime != null)
                {
                    result.Add($"End: {transcriptData.endTime}");
                }

                if (transcriptData.locale != null)
                {
                    result.Add($"Language: {transcriptData.locale}");
                }

                // Add participants
                if (transcriptData.participants != null)
                {
                    result.Add("");
                    result.Add("Participants:");
                    foreach (var participant in transcriptData.participants)
                    {
                        if (participant.displayName != null)
                        {
                            result.Add($"- {participant.displayName?.ToString()}");
                        }
                    }
                }

                result.Add("");
                result.Add("--------------------------------------------------------------");
                result.Add("");

                // Convert ALL transcript segments to readable format
                if (transcriptData.combinedTranscript != null)
                {
                    foreach (var segment in transcriptData.combinedTranscript)
                    {
                        // Skip system messages
                        if (segment.speaker?.id?.ToString() == "system")
                            continue;

                        var speakerName = segment.speaker?.displayName?.ToString() ?? "Unknown Speaker";
                        var text = segment.text?.ToString() ?? "";
                        var timestamp = segment.timestamp?.ToString() ?? "";

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            if (!string.IsNullOrWhiteSpace(timestamp))
                            {
                                result.Add($"[{timestamp}] {speakerName}: {text}");
                            }
                            else
                            {
                                result.Add($"{speakerName}: {text}");
                            }
                            result.Add("");
                        }
                    }
                }

                return string.Join("\n", result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing MS Teams transcript for display: {ex.Message}");
                // Fallback to raw JSON
                return Newtonsoft.Json.JsonConvert.SerializeObject(transcriptData, Newtonsoft.Json.Formatting.Indented);
            }
        }
    }
}
