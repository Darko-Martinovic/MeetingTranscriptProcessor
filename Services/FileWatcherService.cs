using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Service for monitoring the incoming directory for new transcript files
/// </summary>
public class FileWatcherService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly string _incomingPath;
    private readonly string _processingPath;
    private readonly ILogger? _logger;
    private bool _disposed = false;

    public event EventHandler<FileDetectedEventArgs>? FileDetected;

    public FileWatcherService(string incomingPath, string processingPath, ILogger? logger = null)
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
            _logger?.LogInformation($"📁 File watcher started - monitoring: {_incomingPath}");
            Console.WriteLine($"📁 File watcher started - monitoring: {_incomingPath}");

            // Process any existing files
            ProcessExistingFiles();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start file watcher");
            Console.WriteLine($"❌ Failed to start file watcher: {ex.Message}");
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
            Console.WriteLine("📁 File watcher stopped");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping file watcher");
            Console.WriteLine($"❌ Error stopping file watcher: {ex.Message}");
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
                Console.WriteLine($"📄 Found {existingFiles.Count} existing file(s) to process");

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
            Console.WriteLine($"❌ Error processing existing files: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles file creation events
    /// </summary>
    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        _logger?.LogInformation($"File created: {e.FullPath}");

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
            _logger?.LogInformation($"File changed: {e.FullPath}");

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

            // Check if file is locked (still being written)
            if (IsFileLocked(filePath))
            {
                _logger?.LogWarning($"File is locked, will retry: {filePath}");
                // Could implement retry logic here
                return;
            }

            Console.WriteLine($"📄 New file detected: {Path.GetFileName(filePath)}");

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
            _logger?.LogInformation($"Moved file to processing: {processingFilePath}");

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
            _logger?.LogError(ex, $"Error processing file: {filePath}");
            Console.WriteLine(
                $"❌ Error processing file {Path.GetFileName(filePath)}: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Checks if the file type is supported for transcript processing
    /// </summary>
    private static bool IsSupportedFileType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".txt" => true,
            ".md" => true,
            ".json" => true,
            ".xml" => true,
            ".docx" => true,
            ".pdf" => true,
            _ => false
        };
    }

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

/// <summary>
/// Simple logger interface for file watcher
/// </summary>
public interface ILogger
{
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(Exception ex, string message);
}

/// <summary>
/// Simple console logger implementation
/// </summary>
public class ConsoleLogger : ILogger
{
    public void LogInformation(string message)
    {
        Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} - {message}");
    }

    public void LogWarning(string message)
    {
        Console.WriteLine($"[WARN] {DateTime.Now:HH:mm:ss} - {message}");
    }

    public void LogError(Exception ex, string message)
    {
        Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} - {message}");
        Console.WriteLine($"Exception: {ex.Message}");
    }
}
