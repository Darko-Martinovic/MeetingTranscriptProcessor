using System.ComponentModel.DataAnnotations;

namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO for updating meeting title
    /// </summary>
    public class UpdateMeetingTitleDto
    {
        /// <summary>
        /// The new title for the meeting
        /// </summary>
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters")]
        public string Title { get; set; } = string.Empty;
    }
}
