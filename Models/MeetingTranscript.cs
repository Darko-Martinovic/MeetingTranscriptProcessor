namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// Represents a meeting transcript with metadata and content
    /// </summary>
    public class MeetingTranscript
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime MeetingDate { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<string> Participants { get; set; } = new();
        public string? ProjectKey { get; set; }
        public string DetectedLanguage { get; set; } = "en"; // Detected language code (en, fr, nl, etc.)
        public DateTime ProcessedAt { get; set; }
        public string ProcessedBy { get; set; } = "AI Assistant";
        public List<ActionItem> ActionItems { get; set; } = new();
        public List<JiraTicketReference> CreatedJiraTickets { get; set; } = new();
        public TranscriptStatus Status { get; set; } = TranscriptStatus.New;
    }

    /// <summary>
    /// Status of transcript processing
    /// </summary>
    public enum TranscriptStatus
    {
        New,
        Processing,
        Processed,
        Error,
        Archived
    }

    /// <summary>
    /// Represents a reference to a JIRA ticket created from a meeting
    /// </summary>
    public class JiraTicketReference
    {
        public string TicketKey { get; set; } = string.Empty;
        public string TicketUrl { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ActionItemId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Priority { get; set; } = "Medium";
        public string Type { get; set; } = "Task";
        public string? AssignedTo { get; set; }
        public string Status { get; set; } = "Open";
    }
}
