using MeetingTranscriptProcessor.Models;
using MeetingTranscriptProcessor.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeetingTranscriptProcessor.Tests;

/// <summary>
/// A chat client that answers with a set text and usage, and remembers what it was asked
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    public string Answer { get; set; } = "{\"actionItems\": []}";
    public List<(List<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls.Add((messages.ToList(), options));
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer))
        {
            Usage = new UsageDetails { InputTokenCount = 2000, OutputTokenCount = 500, TotalTokenCount = 2500 }
        });
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}

/// <summary>
/// Settings held in memory, for the services that only read them
/// </summary>
public sealed class SettingsOnly(AzureOpenAISettings azureOpenAI) : IConfigurationService
{
    public AzureOpenAISettings GetAzureOpenAISettings() => azureOpenAI;
    public AppConfiguration GetConfiguration() => new() { AzureOpenAI = azureOpenAI };
    public ProcessingSettings GetProcessingSettings() => new();
    public ExtractionSettings GetExtractionSettings() => new();
    public MeetingTypeSettings GetMeetingTypeSettings() => new();
    public LanguageSettings GetLanguageSettings() => new();
    public PromptSettings GetPromptSettings() => new();
    public void ReloadConfiguration() { }
    public Task SaveConfigurationAsync() => Task.CompletedTask;
    public Task SaveAzureOpenAISettingsAsync(AzureOpenAISettings settings) => Task.CompletedTask;
    public Task SaveProcessingSettingsAsync(ProcessingSettings settings) => Task.CompletedTask;
    public string GetExtractionPrompt(MeetingTranscript transcript, string? meetingType = null, string? language = null) => "";
    public string GetSystemPrompt(string? language = null) => azureOpenAI.SystemPrompt;
}

public class AzureOpenAIServiceTests
{
    private static AzureOpenAISettings Configured() => new()
    {
        Endpoint = "https://example.openai.azure.com/",
        ApiKey = "key",
        DeploymentName = "gpt-4o",
        Temperature = 0.1,
        TopP = 0.95,
        MaxTokens = 4000,
        SystemPrompt = "Answer in JSON.",
        InputPricePer1K = 0.0025m,
        OutputPricePer1K = 0.01m
    };

    private static (AzureOpenAIService Service, FakeChatClient Chat, List<string> Built) Create(AzureOpenAISettings settings)
    {
        var chat = new FakeChatClient();
        var built = new List<string>();
        var service = new AzureOpenAIService(
            new SettingsOnly(settings),
            NullLogger<AzureOpenAIService>.Instance,
            s =>
            {
                built.Add(s.DeploymentName);
                return chat;
            });
        return (service, chat, built);
    }

    [Fact]
    public async Task The_model_gets_the_system_prompt_the_settings_and_the_prompt()
    {
        var settings = Configured();
        settings.CustomPrompt = "Only explicit items.";
        var (service, chat, _) = Create(settings);

        await service.ProcessTranscriptAsync("Extract the action items as JSON.");

        var (messages, options) = Assert.Single(chat.Calls);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.StartsWith("Answer in JSON.", messages[0].Text);
        Assert.Contains("Only explicit items.", messages[0].Text);
        Assert.Equal("Extract the action items as JSON.", messages[1].Text);
        Assert.Equal(0.1f, options!.Temperature);
        Assert.Equal(0.95f, options.TopP);
        Assert.Equal(4000, options.MaxOutputTokens);
    }

    [Fact]
    public async Task A_temperature_given_for_one_call_is_used_for_it()
    {
        var (service, chat, _) = Create(Configured());

        await service.ProcessTranscriptAsync("prompt", temperature: 0.7);

        Assert.Equal(0.7f, Assert.Single(chat.Calls).Options!.Temperature);
    }

    [Fact]
    public async Task Tokens_and_cost_come_from_the_usage()
    {
        var (service, chat, _) = Create(Configured());
        chat.Answer = "{\"actionItems\": [{\"title\": \"Fix the build\"}]}";

        var result = await service.ProcessTranscriptAsync("prompt");

        Assert.Equal(chat.Answer, result.Content);
        Assert.Equal(2500, result.TokensUsed);
        Assert.Equal(2 * 0.0025m + 0.5m * 0.01m, result.EstimatedCost);
    }

    [Fact]
    public async Task The_client_is_built_once_and_again_when_the_deployment_changes()
    {
        var settings = Configured();
        var (service, _, built) = Create(settings);

        await service.ProcessTranscriptAsync("one");
        await service.ProcessTranscriptAsync("two");
        settings.DeploymentName = "gpt-4.1";
        await service.ProcessTranscriptAsync("three");

        Assert.Equal(["gpt-4o", "gpt-4.1"], built);
    }

    [Fact]
    public async Task Without_a_key_extraction_throws_and_a_ticket_is_made_with_rules()
    {
        var settings = Configured();
        settings.ApiKey = null;
        var (service, chat, _) = Create(settings);

        Assert.False(service.IsConfigured());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessTranscriptAsync("prompt"));
        var ticket = await service.FormatJiraTicketAsync("Action item: Fix the build", "The build fails", "", "");
        Assert.Contains("\"title\":\"Fix the build\"", ticket);
        Assert.Empty(chat.Calls);
    }

    [Theory]
    [InlineData("https://name.openai.azure.com", "https://name.openai.azure.com/openai/v1/")]
    [InlineData("https://name.openai.azure.com/", "https://name.openai.azure.com/openai/v1/")]
    [InlineData("https://name.services.ai.azure.com/openai/v1", "https://name.services.ai.azure.com/openai/v1/")]
    [InlineData("https://name.openai.azure.com/openai/v1/", "https://name.openai.azure.com/openai/v1/")]
    public void The_v1_api_is_under_the_resource_endpoint(string endpoint, string v1)
    {
        Assert.Equal(new Uri(v1), AzureOpenAIService.V1Endpoint(endpoint));
    }
}
