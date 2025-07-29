using System.ComponentModel.DataAnnotations;

namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO for moving meeting between folders
    /// </summary>
    public class MoveMeetingDto
    {
        /// <summary>
        /// The target folder type to move the meeting to
        /// </summary>
        [Required(ErrorMessage = "Target folder type is required")]
        public FolderType TargetFolderType { get; set; }
    }
}
