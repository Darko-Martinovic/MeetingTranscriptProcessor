using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Service for monitoring the incoming directory for new transcript files
/// </summary>
public class FileWatcherService : IFileWatcherService, IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly string _incomingPath;
    private readonly string _processingPath;
    private readonly ILogger<FileWatcherService>? _logger;
    private bool _disposed = false;

    public event EventHandler<FileDetectedEventArgs>? FileDetected;

    public FileWatcherService(string incomingPath, string processingPath, ILogger<FileWatcherService>? logger = null)
    {
        _incomingPath = incomingPath ?? throw new ArgumentNullException(nameof(incomingPath));
        _processingPath = processingPath ?? throw new ArgumentNullException(nameof(processingPath));
        _logger = logger;

        // Ensure directories exist
        Directory.CreateDirectory(_incomingPath);
        Directory.CreateDirectory(_processingPath);

        // Configure file watcher
        _watcher = new FileSystemWatcher(_incomingPath)
        {
            Filter = "*.*", // Monitor all files
            NotifyFilter =
                NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = false
        };

        // Subscribe to events
        _watcher.Created += OnFileCreated;
        _watcher.Changed += OnFileChanged;
    }

    /// <summary>
    /// Starts monitoring the incoming directory
    /// </summary>
    public void Start()
    {
        try
        {
            _watcher.EnableRaisingEvents = true;
            _logger?.LogInformation("File watcher started on {Folder}", _incomingPath);

            // Process any existing files
            ProcessExistingFiles();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start file watcher");
            throw;
        }
    }

    /// <summary>
    /// Stops monitoring the incoming directory
    /// </summary>
    public void Stop()
    {
        try
        {
            _watcher.EnableRaisingEvents = false;
            _logger?.LogInformation("File watcher stopped");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping file watcher");
        }
    }

    /// <summary>
    /// Processes any files that already exist in the incoming directory
    /// </summary>
    private void ProcessExistingFiles()
    {
        try
        {
            var existingFiles = Directory
                .GetFiles(_incomingPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => IsSupportedFileType(file))
                .ToList();

            if (existingFiles.Any())
            {
                _logger?.LogInformation("Found {Count} file(s) waiting in {Folder}", existingFiles.Count, _incomingPath);

                foreach (var file in existingFiles)
                {
                    // Small delay to ensure file is fully written
                    Thread.Sleep(100);
                    ProcessFile(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing existing files");
        }
    }

    /// <summary>
    /// Handles file creation events
    /// </summary>
    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        _logger?.LogDebug("File created: {Path}", e.FullPath);

        // Small delay to ensure file is fully written
        Thread.Sleep(500);
        ProcessFile(e.FullPath);
    }

    /// <summary>
    /// Handles file change events
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Only process if it's a supported file type and not currently being processed
        if (IsSupportedFileType(e.FullPath) && !IsFileBeingProcessed(e.FullPath))
        {
            _logger?.LogDebug("File changed: {Path}", e.FullPath);

            // Small delay to ensure file is fully written
            Thread.Sleep(500);
            ProcessFile(e.FullPath);
        }
    }

    /// <summary>
    /// Processes a detected file
    /// </summary>
    private void ProcessFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath) || !IsSupportedFileType(filePath))
            {
                return;
            }

            // A file still being written is locked; wait up to 10 seconds for it
            for (var attempt = 0; IsFileLocked(filePath); attempt++)
            {
                if (attempt == 20)
                {
                    _logger?.LogWarning("{FileName} stayed locked; it will be picked up when it changes", Path.GetFileName(filePath));
                    return;
                }

                Thread.Sleep(500);
            }

            _logger?.LogInformation("New file: {FileName}", Path.GetFileName(filePath));

            // Move file to processing directory
            var fileName = Path.GetFileName(filePath);
            var processingFilePath = Path.Combine(_processingPath, fileName);

            // Handle duplicate names
            var counter = 1;
            while (File.Exists(processingFilePath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                var newFileName = $"{nameWithoutExt}_{counter}{extension}";
                processingFilePath = Path.Combine(_processingPath, newFileName);
                counter++;
            }

            File.Move(filePath, processingFilePath);
            _logger?.LogDebug("Moved {FileName} to the processing folder", Path.GetFileName(processingFilePath));

            // Raise event for file processing
            FileDetected?.Invoke(
                this,
                new FileDetectedEventArgs
                {
                    FilePath = processingFilePath,
                    FileName = Path.GetFileName(processingFilePath),
                    DetectedAt = DateTime.UtcNow
                }
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error picking up {FileName}", Path.GetFileName(filePath));
        }
    }

    /// <summary>
    /// Checks if the file type is supported for transcript processing
    /// </summary>
    private static bool IsSupportedFileType(string filePath) => TranscriptReader.IsSupported(filePath);

    /// <summary>
    /// Checks if a file is currently locked (being written to)
    /// </summary>
    private static bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None
            );
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a file is currently being processed
    /// </summary>
    private bool IsFileBeingProcessed(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var processingFilePath = Path.Combine(_processingPath, fileName);
        return File.Exists(processingFilePath);
    }

    /// <summary>
    /// Disposes of the file watcher resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _watcher?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Event arguments for file detection events
/// </summary>
public class FileDetectedEventArgs : EventArgs
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}
