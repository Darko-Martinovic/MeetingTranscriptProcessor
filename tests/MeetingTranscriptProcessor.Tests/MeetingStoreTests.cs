using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Tests;

/// <summary>
/// Listing, titles and moves, on files put straight into the processing folder (the worker
/// only picks files up from the incoming folder)
/// </summary>
public class MeetingStoreTests(AppFactory app) : IClassFixture<AppFactory>
{
    private string PutInProcessing(string template, string fileName)
    {
        var path = Path.Combine(app.Processing, fileName);
        File.Copy(Repository.Template(template), path, overwrite: true);
        return path;
    }

    private async Task<JsonElement> ProcessingMeetingAsync(HttpClient client, string fileName)
    {
        var meetings = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/meetings/folders/Processing/meetings", TestContext.Current.CancellationToken);
        return meetings.EnumerateArray().Single(m => m.GetProperty("fileName").GetString() == fileName);
    }

    [Fact]
    public async Task A_word_file_is_previewed_as_text_not_as_its_zip_bytes()
    {
        var client = app.CreateClient();
        PutInProcessing("transcript_nl.docx", "preview_nl.docx");

        var meeting = await ProcessingMeetingAsync(client, "preview_nl.docx");

        Assert.DoesNotContain("PK", meeting.GetProperty("previewContent").GetString()![..2]);
        Assert.Contains("John", meeting.GetProperty("participants").EnumerateArray().Select(p => p.GetString()));
        Assert.Equal(0, meeting.GetProperty("actionItemCount").GetInt32());
        Assert.False(meeting.GetProperty("hasJiraTickets").GetBoolean());
    }

    [Fact]
    public async Task An_unprocessed_meeting_is_shown_without_calling_the_model()
    {
        var client = app.CreateClient();
        PutInProcessing("sample_meeting_transcript.txt", "unprocessed.txt");

        var transcript = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/meetings/meeting/unprocessed.txt", TestContext.Current.CancellationToken);

        Assert.Equal("Weekly Team Standup - Project Phoenix", transcript.GetProperty("title").GetString());
        Assert.Empty(transcript.GetProperty("actionItems").EnumerateArray());
    }

    [Fact]
    public async Task The_title_of_a_text_file_is_its_first_line()
    {
        var client = app.CreateClient();
        var path = PutInProcessing("sample_meeting_transcript.txt", "retitle.txt");

        var response = await client.PutAsJsonAsync(
            "/api/v1/meetings/meeting/retitle.txt/title", new { title = "Phoenix standup" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = await File.ReadAllLinesAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal("Phoenix standup", lines[0]);
        Assert.Equal("Date: 2024-01-15", lines[1].TrimEnd('\r'));
    }

    [Fact]
    public async Task The_title_of_an_unprocessed_word_file_is_not_written_into_it()
    {
        var client = app.CreateClient();
        var path = PutInProcessing("transcript_fr.docx", "retitle.docx");
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        var response = await client.PutAsJsonAsync(
            "/api/v1/meetings/meeting/retitle.docx/title", new { title = "Réunion" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_title_of_a_processed_meeting_is_changed_in_its_metadata()
    {
        var client = app.CreateClient();
        var archived = Path.Combine(app.Archive, "20260101_090000_success_French_meeting_fr.docx");
        File.Copy(Repository.Template("transcript_fr.docx"), archived);
        var before = await File.ReadAllBytesAsync(archived, TestContext.Current.CancellationToken);
        var metadataPath = Path.Combine(app.Archive, "meeting_fr.meta.json");
        await File.WriteAllTextAsync(
            metadataPath,
            JsonSerializer.Serialize(new MeetingTranscript { Title = "Old", Content = "Bonjour" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            TestContext.Current.CancellationToken);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/meetings/meeting/{Path.GetFileName(archived)}/title", new { title = "Réunion d'équipe" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await File.ReadAllBytesAsync(archived, TestContext.Current.CancellationToken));
        var archive = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/meetings/folders/Archive/meetings?language=French", TestContext.Current.CancellationToken);
        var meeting = Assert.Single(archive.EnumerateArray());
        Assert.Equal("Réunion d'équipe", meeting.GetProperty("title").GetString());
        Assert.Equal("success", meeting.GetProperty("status").GetString());
        Assert.Equal("meeting_fr.docx", meeting.GetProperty("originalName").GetString());
    }

    [Fact]
    public async Task A_meeting_cannot_be_moved_to_a_virtual_folder()
    {
        var client = app.CreateClient();
        PutInProcessing("sample_meeting_transcript.txt", "stay.txt");

        var response = await client.PutAsJsonAsync(
            "/api/v1/meetings/meeting/stay.txt/move", new { targetFolderType = FolderType.Recent }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Contains("virtual", body.GetProperty("error").GetString());
        Assert.True(File.Exists(Path.Combine(app.Processing, "stay.txt")));
    }
}
