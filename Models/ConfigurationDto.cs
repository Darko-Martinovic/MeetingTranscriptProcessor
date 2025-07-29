namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO for complete configuration
    /// </summary>
    public class ConfigurationDto
    {
        public AzureOpenAIDto AzureOpenAI { get; set; } = new();
        public ExtractionDto Extraction { get; set; } = new();
        public EnvironmentDto Environment { get; set; } = new();
    }
}
