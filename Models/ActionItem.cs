namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// Represents an action item extracted from a meeting transcript
    /// </summary>
    public class ActionItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public ActionItemPriority Priority { get; set; } = ActionItemPriority.Medium;
        public ActionItemType Type { get; set; } = ActionItemType.Task;
        public string? ProjectKey { get; set; }
        public string? ExistingTicketKey { get; set; }
        public List<string> Labels { get; set; } = new();
        public string Context { get; set; } = string.Empty; // Original text from transcript
        public bool RequiresJiraTicket { get; set; } = true;
        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Priority levels for action items
    /// </summary>
    public enum ActionItemPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Types of action items
    /// </summary>
    public enum ActionItemType
    {
        Task,
        Bug,
        Story,
        Epic,
        Investigation,
        Documentation,
        Review
    }
}
