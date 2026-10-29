namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// The folders a meeting file moves through and the folder of the configuration files.
/// They come from INCOMING_DIRECTORY, PROCESSING_DIRECTORY, ARCHIVE_DIRECTORY and CONFIG_DIRECTORY;
/// a relative path is taken from the content root, which is the folder the app is started from
/// </summary>
public class StoragePaths
{
    public StoragePaths(IConfiguration configuration, IHostEnvironment environment)
    {
        Incoming = Resolve(configuration["INCOMING_DIRECTORY"], "data/Incoming", environment);
        Processing = Resolve(configuration["PROCESSING_DIRECTORY"], "data/Processing", environment);
        Archive = Resolve(configuration["ARCHIVE_DIRECTORY"], "data/Archive", environment);
        Config = Resolve(configuration["CONFIG_DIRECTORY"], "config", environment);

        Directory.CreateDirectory(Incoming);
        Directory.CreateDirectory(Processing);
        Directory.CreateDirectory(Archive);
    }

    public string Incoming { get; }
    public string Processing { get; }
    public string Archive { get; }
    public string Config { get; }

    /// <summary>
    /// The folders that hold meeting files, in the order they are searched
    /// </summary>
    public IReadOnlyList<string> MeetingFolders => [Archive, Incoming, Processing];

    private static string Resolve(string? value, string fallback, IHostEnvironment environment) =>
        Path.GetFullPath(
            string.IsNullOrWhiteSpace(value) ? fallback : value,
            environment.ContentRootPath
        );
}
