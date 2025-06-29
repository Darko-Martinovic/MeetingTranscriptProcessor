namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// Represents a formatted Jira ticket with cleaned up data from AI
    /// </summary>
    public class FormattedTicket
    {
        /// <summary>
        /// Clean, actionable title without prefixes
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description with context
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Priority level determined by AI
        /// </summary>
        public ActionItemPriority Priority { get; set; } = ActionItemPriority.Medium;

        /// <summary>
        /// Type of work determined by AI
        /// </summary>
        public ActionItemType Type { get; set; } = ActionItemType.Task;

        /// <summary>
        /// Labels for the ticket
        /// </summary>
        public string[]? Labels { get; set; }
    }
}
