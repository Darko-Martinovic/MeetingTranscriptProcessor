namespace MeetingTranscriptProcessor.Models
{
    /// <summary>
    /// DTO for API responses representing folder information
    /// </summary>
    public class FolderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public FolderType Type { get; set; }
        public int MeetingCount { get; set; }
    }
}
