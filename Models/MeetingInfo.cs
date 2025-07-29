namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO representing meeting information for list views
    /// </summary>
    public class MeetingInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string OriginalName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PreviewContent { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public FolderType FolderType { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public List<string> Participants { get; set; } = new List<string>();
        public bool HasJiraTickets { get; set; }
        public int ActionItemCount { get; set; }
        public DateTime ProcessingDate { get; set; }
        public DateTime Date { get; set; }
    }
}
