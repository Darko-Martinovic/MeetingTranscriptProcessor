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
            IConfigurationService configurationService)
        {
            _transcriptProcessor = transcriptProcessor;
            _configurationService = configurationService;
            
            // Get paths from environment or use defaults
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName ?? baseDir;
            
            _archivePath = Environment.GetEnvironmentVariable("ARCHIVE_DIRECTORY") ?? 
                          Path.Combine(projectRoot, "Data", "Archive");
            _incomingPath = Environment.GetEnvironmentVariable("INCOMING_DIRECTORY") ?? 
                           Path.Combine(projectRoot, "Data", "Incoming");
            _processingPath = Environment.GetEnvironmentVariable("PROCESSING_DIRECTORY") ?? 
                             Path.Combine(projectRoot, "Data", "Processing");
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
        public async Task<ActionResult<List<MeetingInfo>>> GetMeetingsInFolder(FolderType folderType)
        {
            try
            {
                var folderPath = folderType switch
                {
                    FolderType.Archive => _archivePath,
                    FolderType.Incoming => _incomingPath,
                    FolderType.Processing => _processingPath,
                    _ => throw new ArgumentException("Invalid folder type")
                };

                var meetings = await GetMeetingsFromFolder(folderPath, folderType);
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
                        var transcript = await _transcriptProcessor.ProcessTranscriptAsync(filePath);
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

        private async Task<int> GetMeetingCountInFolder(string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                    return 0;

                var supportedExtensions = new[] { ".txt", ".md", ".json", ".xml", ".docx", ".pdf" };
                var files = Directory.GetFiles(folderPath)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();

                return await Task.FromResult(files.Count);
            }
            catch
            {
                return 0;
            }
        }

        private async Task<List<MeetingInfo>> GetMeetingsFromFolder(string folderPath, FolderType folderType)
        {
            var meetings = new List<MeetingInfo>();

            try
            {
                if (!Directory.Exists(folderPath))
                    return meetings;

                var supportedExtensions = new[] { ".txt", ".md", ".json", ".xml", ".docx", ".pdf" };
                var files = Directory.GetFiles(folderPath)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
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
                        OriginalName = folderType == FolderType.Archive && parts.Length >= 4 
                            ? string.Join("_", parts.Skip(3)) 
                            : fileName,
                        Size = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime,
                        FolderType = folderType,
                        Status = folderType == FolderType.Archive && parts.Length >= 3 
                            ? parts[2] 
                            : "unknown"
                    };

                    // Try to extract basic info without full processing
                    try
                    {
                        var content = await System.IO.File.ReadAllTextAsync(file);
                        meetingInfo.PreviewContent = content.Length > 200 
                            ? content.Substring(0, 200) + "..."
                            : content;
                        
                        // Try to detect meeting title from first few lines
                        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        meetingInfo.Title = lines.FirstOrDefault()?.Trim() ?? "Untitled Meeting";
                        
                        if (meetingInfo.Title.Length > 100)
                            meetingInfo.Title = meetingInfo.Title.Substring(0, 100) + "...";
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
    }

    // DTOs for API responses
    public class FolderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public FolderType Type { get; set; }
        public int MeetingCount { get; set; }
    }

    public class MeetingInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string OriginalName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PreviewContent { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public FolderType FolderType { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public enum FolderType
    {
        Archive,
        Incoming,
        Processing
    }
}
