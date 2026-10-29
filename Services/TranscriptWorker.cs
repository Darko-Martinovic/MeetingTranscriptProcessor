using System.Text.Json;
using System.Threading.Channels;
using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Processes the files that arrive in the incoming folder: extracts the action items,
/// creates the Jira tickets, archives the file and saves its metadata beside it.
/// MAX_CONCURRENT_FILES (1 to 10, 3 by default) files are processed at a time
/// </summary>
public sealed class TranscriptWorker : BackgroundService
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IFileWatcherService _fileWatcher;
    private readonly ITranscriptProcessorService _transcriptProcessor;
    private readonly IJiraTicketService _jiraTicketService;
    private readonly IProcessingStatusService _processingStatus;
    private readonly StoragePaths _paths;
    private readonly ILogger<TranscriptWorker> _logger;
    private readonly int _maxConcurrentFiles;
    private readonly Channel<(FileDetectedEventArgs File, string ProcessingId)> _queue =
        Channel.CreateUnbounded<(FileDetectedEventArgs, string)>();

    public TranscriptWorker(
        IFileWatcherService fileWatcher,
        ITranscriptProcessorService transcriptProcessor,
        IJiraTicketService jiraTicketService,
        IProcessingStatusService processingStatus,
        StoragePaths paths,
        IConfiguration configuration,
        ILogger<TranscriptWorker> logger)
    {
        _fileWatcher = fileWatcher;
        _transcriptProcessor = transcriptProcessor;
        _jiraTicketService = jiraTicketService;
        _processingStatus = processingStatus;
        _paths = paths;
        _logger = logger;
        _maxConcurrentFiles = int.TryParse(configuration["MAX_CONCURRENT_FILES"], out var max) && max is > 0 and <= 10
            ? max
            : 3;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _fileWatcher.FileDetected += OnFileDetected;
        _fileWatcher.Start();
        _logger.LogInformation(
            "Watching {Incoming}, processing up to {Count} files at a time", _paths.Incoming, _maxConcurrentFiles);

        var consumers = Enumerable.Range(0, _maxConcurrentFiles).Select(_ => ConsumeAsync(stoppingToken));
        await Task.WhenAll(consumers);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _fileWatcher.Stop();
        _fileWatcher.FileDetected -= OnFileDetected;
        _queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    private void OnFileDetected(object? sender, FileDetectedEventArgs e)
    {
        var processingId = _processingStatus.StartProcessing(e.FileName);
        _queue.Writer.TryWrite((e, processingId));
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var (file, processingId) in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessFileAsync(file.FilePath, file.FileName, processingId);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The app is stopping
        }
    }

    private async Task ProcessFileAsync(string filePath, string fileName, string processingId)
    {
        try
        {
            _logger.LogInformation("Processing {FileName}", fileName);
            _processingStatus.UpdateStatus(processingId, ProcessingStage.Starting, "Initializing file processing", 5);
            _processingStatus.UpdateStatus(processingId, ProcessingStage.ReadingFile, "Reading and parsing transcript", 15);

            var transcript = await _transcriptProcessor.ProcessTranscriptAsync(filePath);

            if (transcript.Status == TranscriptStatus.Error)
            {
                _logger.LogWarning("Could not process {FileName}", fileName);
                _processingStatus.CompleteProcessing(processingId, false, "Failed to parse transcript");
                ArchiveFile(filePath, "error", transcript.DetectedLanguage);
                return;
            }

            _processingStatus.UpdateStatus(processingId, ProcessingStage.ExtractingActionItems, $"Extracted {transcript.ActionItems.Count} action items", 50);
            _processingStatus.UpdateStatus(processingId, ProcessingStage.CreatingJiraTickets, "Creating JIRA tickets from action items", 70);

            var result = await _jiraTicketService.ProcessActionItemsAsync(transcript);

            _processingStatus.UpdateStatus(processingId, ProcessingStage.Archiving, "Moving files to archive", 85);
            var archivedFilePath = ArchiveFile(filePath, result.Success ? "success" : "error", transcript.DetectedLanguage);

            // The metadata (with the Jira ticket references) is saved beside the archived file
            _processingStatus.UpdateStatus(processingId, ProcessingStage.SavingMetadata, "Saving transcript metadata", 95);
            if (!string.IsNullOrEmpty(archivedFilePath))
            {
                await SaveTranscriptMetadataAsync(transcript, archivedFilePath);
            }

            _processingStatus.UpdateMetrics(processingId, new ProcessingMetrics
            {
                ActionItemsExtracted = transcript.ActionItems.Count,
                JiraTicketsCreated = transcript.CreatedJiraTickets.Count,
                DetectedLanguage = transcript.DetectedLanguage
            });
            _processingStatus.CompleteProcessing(processingId, result.Success);

            _logger.LogInformation(
                "Processed {FileName}: {ActionItems} action items, {Created} tickets created, {Updated} updated, in {Seconds:F1}s{Error}",
                fileName,
                result.ActionItemsFound,
                result.TicketsCreated,
                result.TicketsUpdated,
                result.ProcessingDuration.TotalSeconds,
                string.IsNullOrEmpty(result.ErrorMessage) ? "" : $" ({result.ErrorMessage})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {FileName}", fileName);
            _processingStatus.CompleteProcessing(processingId, false, ex.Message);
            ArchiveFile(filePath, "error");
        }
    }

    private string ArchiveFile(string filePath, string status, string? languageCode = null)
    {
        try
        {
            if (!File.Exists(filePath))
                return "";

            var archivedName = ArchiveNames.ArchivedName(Path.GetFileName(filePath), status, languageCode, DateTime.Now);
            var archivedPath = Path.Combine(_paths.Archive, archivedName);

            File.Move(filePath, archivedPath);
            _logger.LogInformation("Archived {ArchivedName}", archivedName);
            return archivedPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to archive {FileName}", Path.GetFileName(filePath));
            return "";
        }
    }

    private static async Task SaveTranscriptMetadataAsync(MeetingTranscript transcript, string archivedFilePath)
    {
        var metadataPath = Path.Combine(
            Path.GetDirectoryName(archivedFilePath) ?? "",
            ArchiveNames.MetadataName(Path.GetFileName(archivedFilePath)));

        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(transcript, MetadataJsonOptions));
    }
}
