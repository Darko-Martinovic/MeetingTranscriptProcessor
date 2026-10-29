using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MeetingTranscriptProcessor.Tests;

/// <summary>
/// The meetings API, from an upload through processing to the archive
/// </summary>
public class MeetingsApiTests(AppFactory app) : IClassFixture<AppFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static MultipartFormDataContent FileContent(string fileName, byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task The_folders_are_listed()
    {
        var client = app.CreateClient();

        var folders = await client.GetFromJsonAsync<JsonElement>("/api/v1/meetings/folders", TestContext.Current.CancellationToken);

        Assert.Equal(
            ["Archive", "Incoming", "Processing", "Recent", "Favorites"],
            folders.EnumerateArray().Select(folder => folder.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task An_uploaded_file_is_processed_and_archived_with_its_metadata()
    {
        var client = app.CreateClient();
        var bytes = await File.ReadAllBytesAsync(Repository.Template("sample_meeting_transcript.txt"), TestContext.Current.CancellationToken);

        var upload = await client.PostAsync(
            "/api/v1/meetings/upload", FileContent("phoenix.txt", bytes), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var metadata = await AppFactory.WaitForFileAsync(app.Archive, "*phoenix.meta.json");
        var archived = await AppFactory.WaitForFileAsync(app.Archive, "*_phoenix.txt");
        Assert.Matches(@"^\d{8}_\d{6}_success_English_\d{8}_\d{6}_phoenix\.txt$", Path.GetFileName(archived));
        Assert.Empty(Directory.GetFiles(app.Incoming));
        Assert.Empty(Directory.GetFiles(app.Processing));

        var archive = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/meetings/folders/Archive/meetings", TestContext.Current.CancellationToken);
        var meeting = Assert.Single(archive.EnumerateArray(), m => m.GetProperty("fileName").GetString() == Path.GetFileName(archived));
        Assert.Equal("Weekly Team Standup - Project Phoenix", meeting.GetProperty("title").GetString());
        Assert.Equal("success", meeting.GetProperty("status").GetString());

        var transcript = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/meetings/meeting/{Path.GetFileName(archived)}", TestContext.Current.CancellationToken);
        Assert.Equal(
            ["Alice Johnson", "Bob Smith", "Carol Davis", "David Wilson"],
            transcript.GetProperty("participants").EnumerateArray().Select(p => p.GetString()));

        // Without a model or Jira, the rules find the items and the tickets are simulated
        var items = transcript.GetProperty("actionItems").GetArrayLength();
        Assert.True(items > 0);
        var tickets = transcript.GetProperty("createdJiraTickets").EnumerateArray().ToList();
        Assert.Equal(items, tickets.Count);
        Assert.All(tickets, ticket => Assert.StartsWith("[Simulated]", ticket.GetProperty("ticketUrl").GetString()));

        var deleted = await client.DeleteAsync(
            $"/api/v1/meetings/meeting/{Path.GetFileName(archived)}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.False(File.Exists(archived));
        Assert.False(File.Exists(metadata));
    }

    [Fact]
    public async Task A_file_of_another_type_is_refused()
    {
        var client = app.CreateClient();

        var upload = await client.PostAsync(
            "/api/v1/meetings/upload", FileContent("notes.exe", [1, 2, 3]), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
        Assert.Empty(Directory.GetFiles(app.Incoming));
    }

    [Fact]
    public async Task A_missing_meeting_is_not_found()
    {
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/v1/meetings/meeting/nothing.txt", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
