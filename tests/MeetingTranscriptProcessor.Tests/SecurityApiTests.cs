using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace MeetingTranscriptProcessor.Tests;

/// <summary>
/// File names from requests, and credentials over the API
/// </summary>
public class SecurityApiTests(AppFactory app) : IClassFixture<AppFactory>
{
    private static MultipartFormDataContent FileContent(string fileName, string text)
    {
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(text));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Theory]
    [InlineData("..%5Csecret.txt")]
    [InlineData("..%5C..%5Csecret.txt")]
    [InlineData("C%3Asecret.txt")]
    [InlineData("..")]
    public async Task A_file_name_that_leaves_the_folders_is_refused_on_every_route(string fileName)
    {
        var client = app.CreateClient();
        var secret = Path.Combine(app.Root, "secret.txt");
        await File.WriteAllTextAsync(secret, "secret", TestContext.Current.CancellationToken);
        var token = TestContext.Current.CancellationToken;

        var responses = new[]
        {
            await client.GetAsync($"/api/v1/meetings/meeting/{fileName}", token),
            await client.GetAsync($"/api/v1/meetings/meeting/{fileName}/tickets", token),
            await client.DeleteAsync($"/api/v1/meetings/meeting/{fileName}", token),
            await client.PutAsJsonAsync($"/api/v1/meetings/meeting/{fileName}/title", new { title = "Owned" }, token),
            await client.PutAsJsonAsync($"/api/v1/meetings/meeting/{fileName}/move", new { targetFolderType = 0 }, token),
        };

        Assert.All(responses, response =>
            Assert.True(
                (int)response.StatusCode is >= 400 and < 500,
                $"{response.RequestMessage!.Method} {response.RequestMessage.RequestUri}: {response.StatusCode}"));
        Assert.Equal("secret", await File.ReadAllTextAsync(secret, token));
    }

    [Fact]
    public async Task An_upload_with_a_folder_in_its_name_is_saved_in_the_incoming_folder()
    {
        var client = app.CreateClient();

        var upload = await client.PostAsync(
            "/api/v1/meetings/upload", FileContent("../../escaped.txt", "Meeting: Escape"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var body = await upload.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Matches(@"^\d{8}_\d{6}_escaped\.txt$", body.GetProperty("fileName").GetString());
        Assert.False(File.Exists(Path.Combine(app.Root, "escaped.txt")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(app.Root)!, "escaped.txt")));
    }

    [Fact]
    public async Task Jira_credentials_cannot_be_set_over_the_api()
    {
        var client = app.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/v1/configuration/jira", new { url = "https://evil.example", apiToken = "x" }, TestContext.Current.CancellationToken);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, response.StatusCode.ToString());
        Assert.Null(Environment.GetEnvironmentVariable("JIRA_URL"));
    }

    [Fact]
    public async Task The_model_settings_are_saved_without_the_endpoint_or_the_key()
    {
        var client = app.CreateClient();
        var token = TestContext.Current.CancellationToken;

        var response = await client.PutAsJsonAsync(
            "/api/v1/configuration/azure-openai",
            new { endpoint = "https://evil.example", apiKey = "stolen", deploymentName = "gpt-test", temperature = 0.4, maxTokens = 1234, topP = 0.8 },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var configuration = await client.GetFromJsonAsync<JsonElement>("/api/v1/configuration", token);
        var azure = configuration.GetProperty("azureOpenAI");
        Assert.Equal("", azure.GetProperty("endpoint").GetString());
        Assert.Equal("gpt-test", azure.GetProperty("deploymentName").GetString());
        Assert.Equal(0.4, azure.GetProperty("temperature").GetDouble());
        Assert.Equal(1234, azure.GetProperty("maxTokens").GetInt32());
        Assert.False(azure.GetProperty("isConfigured").GetBoolean());

        var saved = await File.ReadAllTextAsync(Path.Combine(app.Config, "azure-openai.json"), token);
        Assert.DoesNotContain("stolen", saved);
        Assert.DoesNotContain("evil.example", saved);
        Assert.DoesNotContain("apiKey", saved);
    }

    [Fact]
    public async Task A_model_setting_out_of_range_is_refused()
    {
        var client = app.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/v1/configuration/azure-openai", new { temperature = 5 }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_training_upload_keeps_only_its_file_name()
    {
        var client = app.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/training/process",
            FileContent("../../training.txt", "Meeting: Training\nParticipants: Ann\nAnn: I will fix the login bug."),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("training.txt", body.GetProperty("fileName").GetString());
    }
}

/// <summary>
/// The app with API_KEY set
/// </summary>
public sealed class KeyedAppFactory : AppFactory
{
    public const string Key = "test-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("API_KEY", Key);
    }
}

public class ApiKeyTests(KeyedAppFactory app) : IClassFixture<KeyedAppFactory>
{
    [Fact]
    public async Task A_request_without_the_key_is_refused()
    {
        var response = await app.CreateClient().GetAsync("/api/v1/meetings/folders", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_request_with_a_wrong_key_is_refused()
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong");

        var response = await client.GetAsync("/api/v1/meetings/folders", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_request_with_the_key_is_served()
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", KeyedAppFactory.Key);

        var response = await client.GetAsync("/api/v1/meetings/folders", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_cors_preflight_needs_no_key()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/meetings/folders");
        request.Headers.Add("Origin", "http://localhost:5175");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "x-api-key");

        var response = await app.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, response.StatusCode.ToString());
        Assert.Equal("http://localhost:5175", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}
