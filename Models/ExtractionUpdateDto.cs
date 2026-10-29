using System.ComponentModel.DataAnnotations;

namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO for updating the processing settings; a value left out is not changed
    /// </summary>
    public class ExtractionUpdateDto
    {
        [Range(1, 10)]
        public int? MaxConcurrentFiles { get; set; }

        [Range(0.0, 1.0)]
        public double? ValidationConfidenceThreshold { get; set; }

        public bool? EnableValidation { get; set; }
        public bool? EnableHallucinationDetection { get; set; }
        public bool? EnableConsistencyManagement { get; set; }
    }
}
