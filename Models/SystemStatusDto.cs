namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO for system status information
    /// </summary>
    public class SystemStatusDto
    {
        public bool IsRunning { get; set; }
        public bool AzureOpenAIConfigured { get; set; }
        public bool JiraConfigured { get; set; }
        public bool ValidationEnabled { get; set; }
        public bool HallucinationDetectionEnabled { get; set; }
        public bool ConsistencyManagementEnabled { get; set; }
        public DateTime CurrentTime { get; set; }
    }
}
