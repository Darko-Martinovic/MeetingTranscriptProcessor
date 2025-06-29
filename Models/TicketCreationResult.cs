namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// Result of Jira ticket operations
    /// </summary>
    public class TicketCreationResult
    {
        public bool Success { get; set; }
        public string? TicketKey { get; set; }
        public string? TicketUrl { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public TicketOperation Operation { get; set; }
        public string ActionItemId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Type of ticket operation performed
    /// </summary>
    public enum TicketOperation
    {
        Created,
        Updated,
        Linked,
        Commented,
        Skipped
    }

    /// <summary>
    /// Summary of all ticket operations for a transcript
    /// </summary>
    public class TranscriptProcessingResult
    {
        public string TranscriptId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int ActionItemsFound { get; set; }
        public int TicketsCreated { get; set; }
        public int TicketsUpdated { get; set; }
        public List<TicketCreationResult> TicketResults { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan ProcessingDuration { get; set; }
    }
}
