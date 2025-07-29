namespace MeetingTranscriptProcessor.Controllers
{
    // DTOs for configuration
    public class ConfigurationDto
    {
        public AzureOpenAIDto AzureOpenAI { get; set; } = new();
        public ExtractionDto Extraction { get; set; } = new();
        public EnvironmentDto Environment { get; set; } = new();
    }
}
