namespace MeetingTranscriptProcessor.Controllers
{
    public class AzureOpenAIDto
    {
        public string Endpoint { get; set; } = string.Empty;
        public string DeploymentName { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public bool IsConfigured { get; set; }
    }
}
