using System.Threading.Channels;
using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Processes the files that arrive in the incoming folder: extracts the action items,
/// creates the Jira tickets, archives the file and saves its metadata beside it.
/// Up to the MaxConcurrentFiles of the processing settings (1 to 10) are processed at a time;
/// a change on the Settings page applies to the next file
/// </summary>
public sealed class TranscriptWorker : BackgroundService
{
    private const int MaxLimit = 10;

    private readonly IFileWatcherService _fileWatcher;
    private readonly ITranscriptProcessorService _transcriptProcessor;
    private readonly IJiraTicketService _jiraTicketService;
    private readonly IProcessingStatusService _processingStatus;
    private readonly IConfigurationService _configService;
    private readonly MeetingStore _store;
    private readonly ILogger<TranscriptWorker> _logger;
    private readonly Channel<(FileDetectedEventArgs File, string ProcessingId)> _queue =
        Channel.CreateUnbounded<(FileDetectedEventArgs, string)>();

    // Signalled whenever a file finishes, so the loop can start the next one
    private readonly SemaphoreSlim _finished = new(0);
    private int _running;

    public TranscriptWorker(
        IFileWatcherService fileWatcher,
        ITranscriptProcessorService transcriptProcessor,
        IJiraTicketService jiraTicketService,
        IProcessingStatusService processingStatus,
        IConfigurationService configService,
        MeetingStore store,
        ILogger<TranscriptWorker> logger)
    {
        _fileWatcher = fileWatcher;
        _transcriptProcessor = transcriptProcessor;
        _jiraTicketService = jiraTicketService;
        _processingStatus = processingStatus;
        _configService = configService;
        _store = store;
        _logger = logger;
    }

    private int Limit => Math.Clamp(_configService.GetProcessingSettings().MaxConcurrentFiles, 1, MaxLimit);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _fileWatcher.FileDetected += OnFileDetected;
        _fileWatcher.Start();

        var running = new List<Task>();
        try
        {
            await foreach (var (file, processingId) in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                // Wait for a free slot; the timeout notices a limit raised on the Settings page
                while (Volatile.Read(ref _running) >= Limit)
                {
                    await _finished.WaitAsync(TimeSpan.FromSeconds(1), stoppingToken);
                }

                Interlocked.Increment(ref _running);
                running.RemoveAll(task => task.IsCompleted);
                running.Add(Task.Run(async () =>
                {
                    try
                    {
                        await ProcessFileAsync(file.FilePath, file.FileName, processingId);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _running);
                        _finished.Release();
                    }
                }, CancellationToken.None));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The app is stopping; the files being processed are finished first
        }

        await Task.WhenAll(running);
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

    private async Task ProcessFileAsync(string filePath, string fileName, string processingId)
    {
        try
        {
            _processingStatus.UpdateStatus(processingId, ProcessingStage.Starting, "Initializing file processing", 5);
            _processingStatus.UpdateStatus(processingId, ProcessingStage.ReadingFile, "Reading and parsing transcript", 15);

            var transcript = await _transcriptProcessor.ProcessTranscriptAsync(filePath);

            if (transcript.Status == TranscriptStatus.Error)
            {
                _processingStatus.CompleteProcessing(processingId, false, "Failed to parse transcript");
                _store.Archive(filePath, "error", transcript.DetectedLanguage);
                return;
            }

            _processingStatus.UpdateStatus(processingId, ProcessingStage.ExtractingActionItems, $"Extracted {transcript.ActionItems.Count} action items", 50);
            _processingStatus.UpdateStatus(processingId, ProcessingStage.CreatingJiraTickets, "Creating JIRA tickets from action items", 70);

            var result = await _jiraTicketService.ProcessActionItemsAsync(transcript);

            _processingStatus.UpdateStatus(processingId, ProcessingStage.Archiving, "Moving files to archive", 85);
            var archivedFilePath = _store.Archive(filePath, result.Success ? "success" : "error", transcript.DetectedLanguage);

            // The metadata (with the Jira ticket references) is saved beside the archived file
            _processingStatus.UpdateStatus(processingId, ProcessingStage.SavingMetadata, "Saving transcript metadata", 95);
            if (archivedFilePath != null)
            {
                await _store.SaveMetadataAsync(transcript, archivedFilePath);
            }

            _processingStatus.UpdateMetrics(processingId, new ProcessingMetrics
            {
                ActionItemsExtracted = transcript.ActionItems.Count,
                JiraTicketsCreated = transcript.CreatedJiraTickets.Count,
                DetectedLanguage = transcript.DetectedLanguage
            });
            _processingStatus.CompleteProcessing(processingId, result.Success, result.ErrorMessage);

            _logger.LogInformation(
                "Processed {FileName}: {ActionItems} action items, {Created} tickets created, {Updated} updated{Error}",
                fileName,
                result.ActionItemsFound,
                result.TicketsCreated,
                result.TicketsUpdated,
                string.IsNullOrEmpty(result.ErrorMessage) ? "" : $" ({result.ErrorMessage})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {FileName}", fileName);
            _processingStatus.CompleteProcessing(processingId, false, ex.Message);
            _store.Archive(filePath, "error");
        }
    }
}
