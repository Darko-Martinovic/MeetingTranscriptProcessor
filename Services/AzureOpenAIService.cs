using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using MeetingTranscriptProcessor.Models;
using Microsoft.Extensions.AI;
using OpenAI;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Result of Azure OpenAI processing with usage metrics
/// </summary>
public class OpenAIResult
{
    public string Content { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
}

/// <summary>
/// Calls the model through Microsoft.Extensions.AI. The chat client is built from the settings
/// (AOAI_ENDPOINT, AOAI_APIKEY and the deployment) against Azure OpenAI's v1 API, and rebuilt when
/// the deployment changes on the Settings page
/// </summary>
public class AzureOpenAIService : IAzureOpenAIService
{
    private const string JiraSystemPrompt =
        "You are an expert at creating well-formatted Jira tickets in ENGLISH ONLY. Regardless of the input language, you must always translate and create clear, actionable ticket titles and descriptions in English. You always respond with valid JSON format.";

    private static readonly string[] ActionPatterns =
    [
        @"I'll\s+(.*?)(?:\.|,|$)",
        @"I\s+will\s+(.*?)(?:\.|,|$)",
        @"create\s+(.*?)(?:\.|,|$)",
        @"fix\s+(.*?)(?:\.|,|$)",
        @"investigate\s+(.*?)(?:\.|,|$)",
        @"review\s+(.*?)(?:\.|,|$)",
        @"update\s+(.*?)(?:\.|,|$)",
        @"implement\s+(.*?)(?:\.|,|$)"
    ];

    private readonly IConfigurationService _configService;
    private readonly Func<AzureOpenAISettings, IChatClient> _createChatClient;
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly Lock _clientLock = new();
    private (string Key, IChatClient Client)? _chatClient;

    public AzureOpenAIService(
        IConfigurationService configService,
        ILogger<AzureOpenAIService> logger,
        Func<AzureOpenAISettings, IChatClient>? createChatClient = null)
    {
        _configService = configService;
        _logger = logger;
        _createChatClient = createChatClient ?? CreateChatClient;
    }

    /// <summary>
    /// A chat client for the deployment on Azure OpenAI's v1 API, with the key in the request
    /// </summary>
    public static IChatClient CreateChatClient(AzureOpenAISettings settings) =>
        new OpenAIClient(
                new ApiKeyCredential(settings.ApiKey!),
                new OpenAIClientOptions { Endpoint = V1Endpoint(settings.Endpoint!) })
            .GetChatClient(settings.DeploymentName)
            .AsIChatClient();

    /// <summary>
    /// The v1 API under a resource endpoint: https://name.openai.azure.com becomes
    /// https://name.openai.azure.com/openai/v1/; an endpoint that already ends in /openai/v1 is kept
    /// </summary>
    public static Uri V1Endpoint(string endpoint)
    {
        var trimmed = endpoint.Trim().TrimEnd('/');
        return new Uri(trimmed.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase) ? trimmed + "/" : trimmed + "/openai/v1/");
    }

    /// <summary>
    /// True when the endpoint, the key and the deployment are set
    /// </summary>
    public bool IsConfigured()
    {
        var settings = _configService.GetAzureOpenAISettings();
        return !string.IsNullOrEmpty(settings.Endpoint)
            && !string.IsNullOrEmpty(settings.ApiKey)
            && !string.IsNullOrEmpty(settings.DeploymentName);
    }

    /// <summary>
    /// Asks the model to extract action items, at the given temperature or the configured one.
    /// Throws when the model isn't configured or the call fails; the caller falls back to rules
    /// </summary>
    public async Task<OpenAIResult> ProcessTranscriptAsync(string prompt, double? temperature = null)
    {
        var settings = _configService.GetAzureOpenAISettings();
        var systemPrompt = string.IsNullOrWhiteSpace(settings.CustomPrompt)
            ? settings.SystemPrompt
            : $"{settings.SystemPrompt}\n\nAdditional guidance based on training feedback:\n{settings.CustomPrompt}";

        var response = await ChatClient(settings).GetResponseAsync(
            [new ChatMessage(ChatRole.System, systemPrompt), new ChatMessage(ChatRole.User, prompt)],
            new ChatOptions
            {
                Temperature = (float)(temperature ?? settings.Temperature),
                TopP = (float)settings.TopP,
                MaxOutputTokens = settings.MaxTokens
            });

        var inputTokens = response.Usage?.InputTokenCount ?? 0;
        var outputTokens = response.Usage?.OutputTokenCount ?? 0;
        var result = new OpenAIResult
        {
            Content = response.Text,
            TokensUsed = (int)(response.Usage?.TotalTokenCount ?? inputTokens + outputTokens),
            EstimatedCost = inputTokens / 1000m * settings.InputPricePer1K + outputTokens / 1000m * settings.OutputPricePer1K
        };

        _logger.LogInformation(
            "The model answered: {Tokens} tokens, about ${Cost:F4}", result.TokensUsed, result.EstimatedCost);
        return result;
    }

    /// <summary>
    /// Asks the model for a clean, English Jira ticket (JSON with title, description, priority, type, labels);
    /// without a model, or when the call fails, the ticket is made from the action item with rules
    /// </summary>
    public async Task<string> FormatJiraTicketAsync(
        string actionItemTitle,
        string actionItemDescription,
        string meetingContext,
        string participants)
    {
        if (!IsConfigured())
        {
            return FormatJiraTicketFallback(actionItemTitle, actionItemDescription);
        }

        var prompt = $@"
You are an expert at creating well-formatted Jira tickets. Your task is to create a clean, actionable Jira ticket from the following meeting action item.

CRITICAL RULES:
1. ALL OUTPUT MUST BE IN ENGLISH - translate if necessary
2. Create a NEW, CLEAN title - do NOT use the raw input title if it contains conversation snippets
3. If the input title looks like a conversation snippet (contains quotes, colons, or dialogue), extract the actual action from it
4. The title should be a clear action verb + object (e.g. 'Fix authentication bug', 'Create user documentation', 'Investigate performance issue')

INPUT DATA:
Action Item Title: {actionItemTitle}
Action Item Description: {actionItemDescription}
Meeting Context: {meetingContext}
Meeting Participants: {participants}

ANALYSIS TASK:
- If the title contains conversation snippets like 'context: Person: I will do something...', extract the actual action
- If the title is just dialogue, create a proper action-oriented title from the description/context
- Focus on WHAT needs to be done, not WHO said it

Please provide a response in this exact JSON format (ALL TEXT MUST BE IN ENGLISH):
{{
  ""title"": ""Clean, actionable title starting with an action verb (max 60 characters)"",
  ""description"": ""Detailed description with context and requirements"",
  ""priority"": ""High/Medium/Low"",
  ""type"": ""Task/Bug/Story/Investigation/Documentation/Review"",
  ""labels"": [""meeting-generated"", ""action-item""]
}}

EXAMPLES OF GOOD TITLES:
- ""Fix authentication timeout issue""
- ""Create API documentation for users""
- ""Investigate database performance degradation""
- ""Update deployment pipeline configuration""
- ""Review security compliance requirements""

EXAMPLES OF BAD TITLES TO AVOID:
- ""context: John: I'll fix the bug...""
- ""John said he will look into it""
- ""We discussed the API issue""
";

        try
        {
            var response = await ChatClient(_configService.GetAzureOpenAISettings()).GetResponseAsync(
                [new ChatMessage(ChatRole.System, JiraSystemPrompt), new ChatMessage(ChatRole.User, prompt)],
                new ChatOptions { Temperature = 0.2f, TopP = 0.9f, MaxOutputTokens = 1500 });
            return response.Text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Formatting the Jira ticket with the model failed; using rules");
            return FormatJiraTicketFallback(actionItemTitle, actionItemDescription);
        }
    }

    private IChatClient ChatClient(AzureOpenAISettings settings)
    {
        if (!IsConfigured())
        {
            throw new InvalidOperationException("Azure OpenAI isn't configured: set AOAI_ENDPOINT, AOAI_APIKEY and the deployment");
        }

        // One client per endpoint, deployment and key; a change on the Settings page builds a new one
        var key = $"{settings.Endpoint}|{settings.DeploymentName}|{settings.ApiKey!.GetHashCode()}";
        lock (_clientLock)
        {
            if (_chatClient is not { } current || current.Key != key)
            {
                _chatClient = (key, _createChatClient(settings));
            }

            return _chatClient.Value.Client;
        }
    }

    /// <summary>
    /// A ticket made from the action item: its title without "Action item:" and similar prefixes,
    /// or the action in it when it reads like dialogue
    /// </summary>
    private static string FormatJiraTicketFallback(string title, string description)
    {
        var cleanTitle = title
            .Replace("Create new Jira ticket:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Create Jira ticket:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Action item:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("TODO:", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (cleanTitle.Contains("context\":"))
        {
            cleanTitle = ExtractActionFromConversation(cleanTitle, description);
        }

        if (string.IsNullOrEmpty(cleanTitle))
        {
            cleanTitle = "Review meeting action item";
        }
        else if (cleanTitle.Length > 80)
        {
            cleanTitle = cleanTitle[..77] + "...";
        }

        return JsonSerializer.Serialize(new
        {
            title = cleanTitle,
            description = string.IsNullOrEmpty(description) ? cleanTitle : description,
            priority = "Medium",
            type = "Task",
            labels = new[] { "meeting-generated", "action-item" }
        });
    }

    private static string ExtractActionFromConversation(string conversationTitle, string description)
    {
        foreach (var text in new[] { conversationTitle, description })
        {
            foreach (var pattern in ActionPatterns)
            {
                var match = Regex.Match(text ?? "", pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups[1].Value.Trim().Length > 3)
                {
                    var action = match.Groups[1].Value.Trim();
                    return char.ToUpper(action[0]) + action[1..];
                }
            }
        }

        return "Complete meeting action item";
    }

    /// <summary>
    /// Disposes the chat client
    /// </summary>
    public void Dispose()
    {
        _chatClient?.Client.Dispose();
        GC.SuppressFinalize(this);
    }
}
