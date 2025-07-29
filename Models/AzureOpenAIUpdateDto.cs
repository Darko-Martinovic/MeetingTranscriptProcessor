namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO for updating Azure OpenAI configuration
    /// </summary>
    public class AzureOpenAIUpdateDto
    {
        public string? Endpoint { get; set; }
        public string? ApiKey { get; set; }
        public string? DeploymentName { get; set; }
    }
}
