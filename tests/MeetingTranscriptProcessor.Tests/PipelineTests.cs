using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MeetingTranscriptProcessor.Models;
using MeetingTranscriptProcessor.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MeetingTranscriptProcessor.Tests;

/// <summary>
/// A model that answers with a set text and remembers the temperatures it was asked for
/// </summary>
public sealed class FakeAi : IAzureOpenAIService
{
    public string Answer { get; set; } = "{\"actionItems\": []}";
    public List<double?> Temperatures { get; } = [];

    public Task<OpenAIResult> ProcessTranscriptAsync(string prompt, double? temperature = null)
    {
        lock (Temperatures)
        {
            Temperatures.Add(temperature);
        }

        return Task.FromResult(new OpenAIResult { Content = Answer, TokensUsed = 42, EstimatedCost = 0.01m });
    }

    public Task<string> FormatJiraTicketAsync(string title, string description, string context, string participants) =>
        Task.FromResult(JsonSerializer.Serialize(new { title, description, priority = "High", type = "Bug", labels = new[] { "meeting" } }));

    public bool IsConfigured() => true;

    public void Dispose()
    {
    }
}

/// <summary>
/// Jira that answers every request with a set status and body, and remembers the requests
/// </summary>
public sealed class FakeJira : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
    public string Body { get; set; } = "{\"key\": \"MTP-1\"}";
    public List<string> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var content = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests)
        {
            Requests.Add($"{request.Method} {request.RequestUri} {content}");
        }

        return new HttpResponseMessage(Status) { Content = new StringContent(Body) };
    }
}

/// <summary>
/// The app with the fake model, a fake Jira at https://jira.example, and the checks on the model's
/// items turned off, so every item it answers with is kept
/// </summary>
public sealed class PipelineAppFactory : AppFactory
{
    public FakeAi Ai { get; } = new();
    public FakeJira Jira { get; } = new();

    public PipelineAppFactory()
    {
        File.WriteAllText(
            Path.Combine(Config, "processing.json"),
            JsonSerializer.Serialize(new ProcessingSettings
            {
                EnableValidation = false,
                EnableHallucinationDetection = false,
                EnableConsistencyManagement = false
            }));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("JIRA_URL", "https://jira.example");
        builder.UseSetting("JIRA_EMAIL", "bot@example.com");
        builder.UseSetting("JIRA_API_TOKEN", "token");
        builder.UseSetting("JIRA_PROJECT_KEY", "MTP");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IAzureOpenAIService>(Ai);
            services.AddHttpClient(JiraTicketService.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Jira);
        });
    }
}

/// <summary>
/// From an uploaded file through the model and Jira to the archive. The tests share one app and its
/// fakes; xUnit runs the tests of a class one after another
/// </summary>
public class PipelineTests(PipelineAppFactory app) : IClassFixture<PipelineAppFactory>
{
    private const string Answer = """
        ```json
        {
          "actionItems": [
            {
              "title": "Fix the password reset emails",
              "description": "Reset emails aren't sent",
              "assignedTo": "Bob Smith",
              "dueDate": "2024-01-17",
              "priority": "High",
              "type": "Bug",
              "context": "Bob: I'll look into it today",
              "requiresJiraTicket": true
            },
            {
              "title": "Schedule the UI review meeting",
              "description": "With the product team",
              "assignedTo": "Carol Davis",
              "priority": "medium",
              "type": "Review",
              "requiresJiraTicket": false
            },
            {
              "title": "Fix the password reset emails",
              "description": "Same item said twice",
              "assignedTo": "Bob Smith",
              "priority": "Low",
              "type": "Bug"
            }
          ]
        }
        ```
        """;

    private static MultipartFormDataContent FileContent(string fileName, byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private async Task<(string Archived, MeetingTranscript Metadata)> UploadAndWaitAsync(string fileName)
    {
        var client = app.CreateClient();
        var bytes = await File.ReadAllBytesAsync(Repository.Template("sample_meeting_transcript.txt"), TestContext.Current.CancellationToken);
        var upload = await client.PostAsync("/api/v1/meetings/upload", FileContent(fileName, bytes), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var metadataPath = await AppFactory.WaitForFileAsync(app.Archive, $"*_{baseName}.meta.json");
        var archived = await AppFactory.WaitForFileAsync(app.Archive, $"*_{baseName}.txt");
        var metadata = JsonSerializer.Deserialize<MeetingTranscript>(
            await File.ReadAllTextAsync(metadataPath, TestContext.Current.CancellationToken),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return (archived, metadata);
    }

    [Fact]
    public async Task The_models_items_are_parsed_merged_and_sent_to_jira()
    {
        app.Ai.Answer = Answer;
        app.Jira.Status = HttpStatusCode.Created;
        app.Jira.Body = "{\"key\": \"MTP-7\"}";

        var (archived, metadata) = await UploadAndWaitAsync("jira_ok.txt");

        Assert.Matches(@"_success_English_", Path.GetFileName(archived));
        Assert.Equal(2, metadata.ActionItems.Count);
        var bug = metadata.ActionItems.Single(item => item.Type == ActionItemType.Bug);
        Assert.Equal("Fix the password reset emails", bug.Title);
        Assert.Equal(ActionItemPriority.High, bug.Priority);
        Assert.Equal("Bob Smith", bug.AssignedTo);
        Assert.Equal(new DateTime(2024, 1, 17), bug.DueDate);
        Assert.Contains("Same item said twice", bug.Description);
        Assert.Equal(42, metadata.TokensUsed);

        // The review needs no ticket, so Jira saw one request
        var ticket = Assert.Single(metadata.CreatedJiraTickets);
        Assert.Equal("MTP-7", ticket.TicketKey);
        Assert.Equal("https://jira.example/browse/MTP-7", ticket.TicketUrl);
        var request = Assert.Single(app.Jira.Requests, r => r.Contains("jira_ok"));
        Assert.StartsWith("POST https://jira.example/rest/api/3/issue ", request);
        Assert.Contains("\"key\":\"MTP\"", request);
    }

    [Fact]
    public async Task A_ticket_jira_refuses_fails_the_meeting_instead_of_being_simulated()
    {
        app.Ai.Answer = Answer;
        app.Jira.Status = HttpStatusCode.BadRequest;
        app.Jira.Body = "{\"errors\": {\"project\": \"project is required\"}}";

        var (archived, metadata) = await UploadAndWaitAsync("jira_refused.txt");

        Assert.Matches(@"_error_English_", Path.GetFileName(archived));
        Assert.Empty(metadata.CreatedJiraTickets);
        Assert.Equal(2, metadata.ActionItems.Count);
    }

    [Fact]
    public async Task An_answer_that_isnt_json_is_read_line_by_line()
    {
        app.Ai.Answer = "Action item: Bob will fix the password reset emails\nAction item: Carol will schedule the review";
        app.Jira.Status = HttpStatusCode.Created;

        var (_, metadata) = await UploadAndWaitAsync("plain_answer.txt");

        Assert.Equal(2, metadata.ActionItems.Count);
        Assert.All(metadata.ActionItems, item => Assert.DoesNotContain("Action item:", item.Title));
    }

    [Fact]
    public async Task The_training_temperature_is_used_for_that_request_only()
    {
        var client = app.CreateClient();
        var before = (await client.GetFromJsonAsync<JsonElement>("/api/v1/configuration", TestContext.Current.CancellationToken))
            .GetProperty("azureOpenAI").GetProperty("temperature").GetDouble();
        var form = FileContent("training.txt", "Meeting: Training\nAnn: I will fix the login bug."u8.ToArray());
        form.Add(new StringContent("0.7"), "temperature");

        var response = await client.PostAsync("/api/v1/training/process", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(0.7, body.GetProperty("temperature").GetDouble());
        Assert.Contains(0.7, app.Ai.Temperatures);
        var after = (await client.GetFromJsonAsync<JsonElement>("/api/v1/configuration", TestContext.Current.CancellationToken))
            .GetProperty("azureOpenAI").GetProperty("temperature").GetDouble();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Processing_settings_apply_to_the_next_file_and_are_saved()
    {
        var client = app.CreateClient();
        var processor = app.Services.GetRequiredService<ITranscriptProcessorService>();
        var copy = Path.Combine(app.Root, "settings.txt");
        File.Copy(Repository.Template("sample_meeting_transcript.txt"), copy, overwrite: true);

        // An item that isn't in the transcript is kept while hallucination detection is off
        app.Ai.Answer = """{"actionItems": [{"title": "Migrate the billing system to Kubernetes", "description": "Not discussed"}]}""";
        Assert.Single((await processor.ProcessTranscriptAsync(copy)).ActionItems);

        var update = await client.PutAsJsonAsync(
            "/api/v1/configuration/extraction",
            new { enableHallucinationDetection = true, validationConfidenceThreshold = 0.9 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        try
        {
            Assert.Empty((await processor.ProcessTranscriptAsync(copy)).ActionItems);
            var saved = await File.ReadAllTextAsync(Path.Combine(app.Config, "processing.json"), TestContext.Current.CancellationToken);
            Assert.Contains("\"enableHallucinationDetection\": true", saved);
        }
        finally
        {
            await client.PutAsJsonAsync(
                "/api/v1/configuration/extraction",
                new { enableHallucinationDetection = false, validationConfidenceThreshold = 0.5 },
                TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task A_processing_setting_out_of_range_is_refused()
    {
        var response = await app.CreateClient().PutAsJsonAsync(
            "/api/v1/configuration/extraction", new { maxConcurrentFiles = 50 }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
