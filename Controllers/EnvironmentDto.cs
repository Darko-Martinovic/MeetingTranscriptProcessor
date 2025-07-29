namespace MeetingTranscriptProcessor.Controllers
{
    public class EnvironmentDto
    {
        public string? IncomingDirectory { get; set; }
        public string? ProcessingDirectory { get; set; }
        public string? ArchiveDirectory { get; set; }
        public string? JiraUrl { get; set; }
        public string? JiraEmail { get; set; }
        public string? JiraDefaultProject { get; set; }
        public bool IsJiraConfigured { get; set; }
    }
}
