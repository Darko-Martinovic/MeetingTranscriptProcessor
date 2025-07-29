namespace MeetingTranscriptProcessor.Controllers
{
    public class AzureOpenAIUpdateDto
    {
        public string? Endpoint { get; set; }
        public string? ApiKey { get; set; }
        public string? DeploymentName { get; set; }
    }
}
