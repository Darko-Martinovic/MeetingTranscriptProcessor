namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO for extraction configuration
    /// </summary>
    public class ExtractionDto
    {
        public int MaxConcurrentFiles { get; set; }
        public double ValidationConfidenceThreshold { get; set; }
        public bool EnableValidation { get; set; }
        public bool EnableHallucinationDetection { get; set; }
        public bool EnableConsistencyManagement { get; set; }
    }
}
