namespace MeetingTranscriptProcessor.Models;

public class ProcessingStatus
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public ProcessingStage Stage { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
    public ProcessingMetrics? Metrics { get; set; }
}

public class ProcessingMetrics
{
    public int ActionItemsExtracted { get; set; }
    public int JiraTicketsCreated { get; set; }
    public string? DetectedLanguage { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}

public enum ProcessingStage
{
    Queued = 0,
    Starting = 1,
    ReadingFile = 2,
    ExtractingActionItems = 3,
    CreatingJiraTickets = 4,
    SavingMetadata = 5,
    Archiving = 6,
    Completed = 7,
    Failed = 8
}

public class ProcessingQueue
{
    public List<ProcessingStatus> CurrentlyProcessing { get; set; } = new();
    public List<ProcessingStatus> RecentlyCompleted { get; set; } = new();
    public int QueueLength { get; set; }
    public bool IsProcessingEnabled { get; set; } = true;
}
