namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO for updating Jira configuration
    /// </summary>
    public class JiraUpdateDto
    {
        public string? Url { get; set; }
        public string? Email { get; set; }
        public string? ApiToken { get; set; }
        public string? DefaultProject { get; set; }
    }
}
