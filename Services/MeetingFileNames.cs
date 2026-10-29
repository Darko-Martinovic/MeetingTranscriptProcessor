namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// File names that come from requests. One is used only when it is a plain name, so a request
/// can never reach a file outside the meeting folders
/// </summary>
public static class MeetingFileNames
{
    private static readonly char[] Invalid =
        [.. Path.GetInvalidFileNameChars(), '/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// A name with no folder, no character a file name can't have on Windows or Linux, and not . or ..
    /// </summary>
    public static bool IsSafe(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.Length <= 255
        && fileName.IndexOfAny(Invalid) < 0
        && fileName.Trim('.', ' ').Length > 0;

    /// <summary>
    /// The name an uploaded file is saved under: the last part of the name the client sent,
    /// or null when that isn't a safe name
    /// </summary>
    public static string? FromUpload(string? clientName)
    {
        var name = clientName?.Split('/', '\\')[^1].Trim();
        return IsSafe(name) ? name : null;
    }

    /// <summary>
    /// The full path of a file in a folder, or null when the name isn't safe or the path would
    /// leave the folder
    /// </summary>
    public static string? PathIn(string folder, string? fileName)
    {
        if (!IsSafe(fileName))
        {
            return null;
        }

        var folderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var filePath = Path.GetFullPath(Path.Combine(folderPath, fileName!));
        return string.Equals(Path.GetDirectoryName(filePath), folderPath, StringComparison.Ordinal) ? filePath : null;
    }
}
