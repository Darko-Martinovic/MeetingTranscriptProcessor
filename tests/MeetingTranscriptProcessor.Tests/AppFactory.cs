using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MeetingTranscriptProcessor.Tests;

/// <summary>
/// The app on folders of its own: the meeting folders and a copy of the configuration files
/// (without azure-openai.json, so no model is ever called) under a temporary folder
/// </summary>
public sealed class AppFactory : WebApplicationFactory<Program>
{
    public AppFactory()
    {
        Root = Directory.CreateTempSubdirectory("mtp-tests-").FullName;
        Incoming = Directory.CreateDirectory(Path.Combine(Root, "Incoming")).FullName;
        Processing = Directory.CreateDirectory(Path.Combine(Root, "Processing")).FullName;
        Archive = Directory.CreateDirectory(Path.Combine(Root, "Archive")).FullName;
        Config = Directory.CreateDirectory(Path.Combine(Root, "config")).FullName;

        foreach (var file in Directory.GetFiles(Path.Combine(Repository.Root, "config"), "*.json"))
        {
            if (Path.GetFileName(file) != "azure-openai.json")
            {
                File.Copy(file, Path.Combine(Config, Path.GetFileName(file)));
            }
        }
    }

    public string Root { get; }
    public string Incoming { get; }
    public string Processing { get; }
    public string Archive { get; }
    public string Config { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("INCOMING_DIRECTORY", Incoming);
        builder.UseSetting("PROCESSING_DIRECTORY", Processing);
        builder.UseSetting("ARCHIVE_DIRECTORY", Archive);
        builder.UseSetting("CONFIG_DIRECTORY", Config);
    }

    /// <summary>
    /// Waits for a file matching the pattern to appear in a folder
    /// </summary>
    public static async Task<string> WaitForFileAsync(string folder, string pattern, int seconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            var match = Directory.GetFiles(folder, pattern).FirstOrDefault();
            if (match != null)
            {
                return match;
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"No {pattern} in {folder} after {seconds}s");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A file the watcher still holds; the temporary folder is cleaned up later
        }
    }
}

/// <summary>
/// Paths in the repository and the process settings every test runs with
/// </summary>
public static class Repository
{
    public static string Root { get; } = FindRoot();

    public static string Template(string name) => Path.Combine(Root, "data", "Templates", name);

    /// <summary>
    /// Runs before any test: the app skips .env in the Testing environment, and the
    /// credentials a developer may have in the environment are cleared, so tests never call
    /// Azure OpenAI or Jira
    /// </summary>
    [ModuleInitializer]
    internal static void SetUpEnvironment()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        foreach (var name in new[]
        {
            "AOAI_ENDPOINT", "AOAI_APIKEY", "CHATCOMPLETION_DEPLOYMENTNAME",
            "AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_API_KEY", "AZURE_OPENAI_DEPLOYMENT_NAME",
            "JIRA_URL", "JIRA_EMAIL", "JIRA_API_TOKEN"
        })
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MeetingTranscriptProcessor.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}
