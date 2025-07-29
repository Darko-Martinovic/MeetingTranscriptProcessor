using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Interface for file watching operations
/// </summary>
public interface IFileWatcherService : IDisposable
{
    event EventHandler<FileDetectedEventArgs>? FileDetected;
    void Start();
    void Stop();
}
