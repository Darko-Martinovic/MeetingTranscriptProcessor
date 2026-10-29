namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Names of archived meeting files (yyyyMMdd_HHmmss_status_Language_originalname)
/// and of the metadata files beside them (originalname.meta.json)
/// </summary>
public static class ArchiveNames
{
    private static readonly string[] LanguageNames = ["English", "French", "Dutch", "Spanish", "German"];

    /// <summary>
    /// The name a processed file gets in the archive
    /// </summary>
    public static string ArchivedName(string fileName, string status, string? languageCode, DateTime processedAt)
    {
        var languageInfo = string.IsNullOrEmpty(languageCode) ? "" : $"_{LanguageName(languageCode)}";
        return $"{processedAt:yyyyMMdd_HHmmss}_{status}{languageInfo}_{fileName}";
    }

    /// <summary>
    /// The name of the metadata file that belongs to a meeting file
    /// </summary>
    public static string MetadataName(string fileName) => $"{BaseName(fileName)}.meta.json";

    /// <summary>
    /// The original name of an archived file, without the timestamp, status, language and extension;
    /// a name that doesn't follow the archive pattern is returned without its extension
    /// </summary>
    public static string BaseName(string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var parts = nameWithoutExt.Split('_');

        if (parts.Length >= 4
            && parts[0].Length == 8 && parts[0].All(char.IsDigit)
            && parts[1].Length == 6 && parts[1].All(char.IsDigit))
        {
            // Skip the timestamp and the status, and the language when there is one
            var skipCount = parts.Length > 4 && IsLanguageName(parts[3]) ? 4 : 3;
            return string.Join("_", parts.Skip(skipCount));
        }

        return nameWithoutExt;
    }

    /// <summary>
    /// The language name used in archived file names
    /// </summary>
    public static string LanguageName(string languageCode) => languageCode.ToLowerInvariant() switch
    {
        "en" => "English",
        "fr" => "French",
        "nl" => "Dutch",
        "es" => "Spanish",
        "de" => "German",
        "pt" => "Portuguese",
        _ => "Unknown"
    };

    private static bool IsLanguageName(string part) =>
        LanguageNames.Contains(part, StringComparer.OrdinalIgnoreCase);
}
