using System.ComponentModel.DataAnnotations;

namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO for updating the model settings of Azure OpenAI; the endpoint and the key come from .env only
    /// </summary>
    public class AzureOpenAIUpdateDto
    {
        public string? DeploymentName { get; set; }

        [Range(0.0, 2.0)]
        public double? Temperature { get; set; }

        [Range(1, 128000)]
        public int? MaxTokens { get; set; }

        [Range(0.0, 1.0)]
        public double? TopP { get; set; }
    }
}
