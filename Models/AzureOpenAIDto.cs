namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO for Azure OpenAI configuration
    /// </summary>
    public class AzureOpenAIDto
    {
        public string Endpoint { get; set; } = string.Empty;
        public string DeploymentName { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public bool IsConfigured { get; set; }
        public string? SystemPrompt { get; set; }
        public string? CustomPrompt { get; set; }
    }
}
