using System.Collections.Concurrent;
using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

public interface IProcessingStatusService
{
    string StartProcessing(string fileName);
    void UpdateStatus(string id, ProcessingStage stage, string message, int progressPercentage = 0);
    void UpdateMetrics(string id, ProcessingMetrics metrics);
    void CompleteProcessing(string id, bool success, string? errorMessage = null);
    ProcessingStatus? GetStatus(string id);
    ProcessingQueue GetQueue();
    void ClearCompleted();
}

public class ProcessingStatusService : IProcessingStatusService
{
    private readonly ConcurrentDictionary<string, ProcessingStatus> _activeProcessing = new();
    private readonly ConcurrentQueue<ProcessingStatus> _recentlyCompleted = new();
    private const int MaxRecentlyCompleted = 10;

    public string StartProcessing(string fileName)
    {
        var id = Guid.NewGuid().ToString("N")[..8]; // Short ID for display
        var status = new ProcessingStatus
        {
            Id = id,
            FileName = fileName,
            Stage = ProcessingStage.Queued,
            StatusMessage = "File queued for processing",
            ProgressPercentage = 0,
            StartedAt = DateTime.Now
        };

        _activeProcessing.TryAdd(id, status);
        Console.WriteLine($"🚀 Started processing: {fileName} (ID: {id})");
        return id;
    }

    public void UpdateStatus(string id, ProcessingStage stage, string message, int progressPercentage = 0)
    {
        if (_activeProcessing.TryGetValue(id, out var status))
        {
            status.Stage = stage;
            status.StatusMessage = message;
            status.ProgressPercentage = progressPercentage;
            
            Console.WriteLine($"📊 {id}: {stage} - {message} ({progressPercentage}%)");
        }
    }

    public void UpdateMetrics(string id, ProcessingMetrics metrics)
    {
        if (_activeProcessing.TryGetValue(id, out var status))
        {
            status.Metrics = metrics;
            Console.WriteLine($"📈 {id}: Metrics - {metrics.ActionItemsExtracted} actions, {metrics.JiraTicketsCreated} tickets");
        }
    }

    public void CompleteProcessing(string id, bool success, string? errorMessage = null)
    {
        if (_activeProcessing.TryRemove(id, out var status))
        {
            status.CompletedAt = DateTime.Now;
            status.HasError = !success;
            status.ErrorMessage = errorMessage;
            status.Stage = success ? ProcessingStage.Completed : ProcessingStage.Failed;
            status.ProgressPercentage = success ? 100 : 0;
            
            if (status.Metrics == null)
            {
                status.Metrics = new ProcessingMetrics();
            }
            status.Metrics.ProcessingTime = status.CompletedAt.Value - status.StartedAt;

            // Add to recently completed (thread-safe)
            _recentlyCompleted.Enqueue(status);
            
            // Keep only the last N completed items
            while (_recentlyCompleted.Count > MaxRecentlyCompleted)
            {
                _recentlyCompleted.TryDequeue(out _);
            }

            var statusIcon = success ? "✅" : "❌";
            var duration = status.Metrics.ProcessingTime.TotalSeconds;
            Console.WriteLine($"{statusIcon} {id}: Completed in {duration:F1}s - {status.FileName}");
        }
    }

    public ProcessingStatus? GetStatus(string id)
    {
        _activeProcessing.TryGetValue(id, out var status);
        return status;
    }

    public ProcessingQueue GetQueue()
    {
        var recentlyCompleted = new List<ProcessingStatus>();
        foreach (var item in _recentlyCompleted)
        {
            recentlyCompleted.Add(item);
        }

        return new ProcessingQueue
        {
            CurrentlyProcessing = _activeProcessing.Values.OrderBy(x => x.StartedAt).ToList(),
            RecentlyCompleted = recentlyCompleted.OrderByDescending(x => x.CompletedAt).ToList(),
            QueueLength = _activeProcessing.Count,
            IsProcessingEnabled = true
        };
    }

    public void ClearCompleted()
    {
        while (_recentlyCompleted.TryDequeue(out _)) { }
        Console.WriteLine("🧹 Cleared completed processing history");
    }
}
