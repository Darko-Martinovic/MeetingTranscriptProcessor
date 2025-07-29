namespace MeetingTranscriptProcessor.Controllers
{
    // DTOs for API responses
    public class FolderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public FolderType Type { get; set; }
        public int MeetingCount { get; set; }
    }
}
