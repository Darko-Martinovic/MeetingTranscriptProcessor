using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Creates Jira tickets from action items, or comments on the ticket an item names.
/// Without JIRA_URL, JIRA_EMAIL and JIRA_API_TOKEN the tickets are simulated; when Jira is
/// configured and refuses a ticket, that ticket fails
/// </summary>
public class JiraTicketService : IJiraTicketService
{
    /// <summary>
    /// The name of the HTTP client for Jira
    /// </summary>
    public const string HttpClientName = "jira";

    private static readonly string[] ConversationPatterns =
    [
        "\"context\":",
        "\"description\":",
        "context\":",
        "description\":",
        "\"title\":",
        "title\":",
        @"\w+:\s+I'll",
        @"\w+:\s+I\s+will",
        @"\w+:\s+We'll",
        @"\w+:\s+We\s+will",
        @"\w+:\s+[A-Z][a-z]+\s+(said|mentioned|discussed)",
        @"^\s*\{",
        @"^\s*""[^""]*"":\s*""",
        @"Create\s+a?\s+Jira\s+ticket",
        @"Create\s+new\s+Jira\s+ticket"
    ];

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

    private readonly string? _jiraBaseUrl;
    private readonly string? _jiraApiToken;
    private readonly string? _jiraUserEmail;
    private readonly string _defaultProjectKey;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAzureOpenAIService _aiService;
    private readonly ILogger<JiraTicketService> _logger;

    public JiraTicketService(
        IAzureOpenAIService aiService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<JiraTicketService> logger)
    {
        _aiService = aiService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _jiraBaseUrl = configuration["JIRA_URL"]?.TrimEnd('/');
        _jiraApiToken = configuration["JIRA_API_TOKEN"];
        _jiraUserEmail = configuration["JIRA_EMAIL"];
        _defaultProjectKey = configuration["JIRA_PROJECT_KEY"] is { Length: > 0 } key ? key
            : configuration["JIRA_DEFAULT_PROJECT"] is { Length: > 0 } project ? project
            : "TASK";

        if (IsJiraConfigured)
            _logger.LogInformation("Jira is configured at {Url}, default project {Project}", _jiraBaseUrl, _defaultProjectKey);
        else
            _logger.LogInformation("Jira isn't configured: tickets are simulated");
    }

    private bool IsJiraConfigured =>
        !string.IsNullOrWhiteSpace(_jiraBaseUrl)
        && !string.IsNullOrWhiteSpace(_jiraApiToken)
        && !string.IsNullOrWhiteSpace(_jiraUserEmail);

    /// <summary>
    /// Processes action items and creates/updates Jira tickets; the result succeeds when every ticket did
    /// </summary>
    public async Task<TranscriptProcessingResult> ProcessActionItemsAsync(MeetingTranscript transcript)
    {
        var started = DateTime.UtcNow;
        var result = new TranscriptProcessingResult
        {
            TranscriptId = transcript.Id,
            FileName = transcript.FileName,
            ActionItemsFound = transcript.ActionItems.Count
        };

        try
        {
            foreach (var actionItem in transcript.ActionItems)
            {
                if (!actionItem.RequiresJiraTicket)
                {
                    result.TicketResults.Add(new TicketCreationResult
                    {
                        Success = true,
                        Operation = TicketOperation.Skipped,
                        Message = "Action item marked as not requiring Jira ticket",
                        ActionItemId = actionItem.Id
                    });
                    continue;
                }

                var ticketResult = string.IsNullOrEmpty(actionItem.ExistingTicketKey)
                    ? await CreateNewTicketAsync(actionItem, transcript)
                    : await UpdateExistingTicketAsync(actionItem);

                if (ticketResult.Success && ticketResult.Operation == TicketOperation.Created)
                    result.TicketsCreated++;
                if (ticketResult.Success && ticketResult.Operation == TicketOperation.Updated)
                    result.TicketsUpdated++;
                result.TicketResults.Add(ticketResult);
            }

            var failed = result.TicketResults.Count(ticket => !ticket.Success);
            result.Success = failed == 0;
            if (failed > 0)
            {
                result.ErrorMessage = $"{failed} of {result.TicketResults.Count} Jira tickets failed";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating the Jira tickets for {FileName}", transcript.FileName);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        result.ProcessingDuration = DateTime.UtcNow - started;
        return result;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_jiraUserEmail}:{_jiraApiToken}")));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private async Task<TicketCreationResult> CreateNewTicketAsync(ActionItem actionItem, MeetingTranscript transcript)
    {
        if (!IsJiraConfigured)
        {
            return Simulate(actionItem, TicketOperation.Created, transcript);
        }

        try
        {
            var ticket = await FormatTicketWithAIAsync(actionItem, transcript);
            if (IsConversationSnippet(ticket.Title))
            {
                ticket = await CleanupTitleWithAIAsync(ticket, actionItem);
            }

            var projectKey = actionItem.ProjectKey ?? transcript.ProjectKey ?? _defaultProjectKey;
            var payload = new
            {
                fields = new
                {
                    project = new { key = projectKey },
                    summary = ticket.Title,
                    description = CreateJiraDescriptionADF(ticket.Description, transcript),
                    issuetype = new { name = MapActionItemTypeToJiraIssueType(ticket.Type) },
                    assignee = string.IsNullOrEmpty(actionItem.AssignedTo) ? null : new { name = actionItem.AssignedTo },
                    duedate = actionItem.DueDate?.ToString("yyyy-MM-dd"),
                    labels = ticket.Labels ?? CreateLabels(actionItem, transcript)
                }
            };

            using var client = CreateClient();
            using var response = await client.PostAsync(
                $"{_jiraBaseUrl}/rest/api/3/issue",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode
                && JsonSerializer.Deserialize<JsonElement>(body).TryGetProperty("key", out var keyElement)
                && keyElement.GetString() is { Length: > 0 } ticketKey)
            {
                var ticketUrl = $"{_jiraBaseUrl}/browse/{ticketKey}";
                transcript.CreatedJiraTickets.Add(new JiraTicketReference
                {
                    TicketKey = ticketKey,
                    TicketUrl = ticketUrl,
                    Title = ticket.Title,
                    ActionItemId = actionItem.Id,
                    CreatedAt = DateTime.UtcNow,
                    Priority = ticket.Priority.ToString(),
                    Type = ticket.Type.ToString(),
                    AssignedTo = actionItem.AssignedTo,
                    Status = "Open"
                });
                _logger.LogInformation("Created Jira ticket {TicketKey} in {Project}", ticketKey, projectKey);

                return new TicketCreationResult
                {
                    Success = true,
                    TicketKey = ticketKey,
                    TicketUrl = ticketUrl,
                    Operation = TicketOperation.Created,
                    Message = $"Successfully created ticket {ticketKey}",
                    ActionItemId = actionItem.Id
                };
            }

            _logger.LogError(
                "Jira refused the ticket for {Title} in {Project}: {Status} {Body}",
                ticket.Title, projectKey, (int)response.StatusCode, body);
            return Failed(actionItem, TicketOperation.Created, $"Jira answered {(int)response.StatusCode}", body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating a Jira ticket for {Title}", actionItem.Title);
            return Failed(actionItem, TicketOperation.Created, "Could not reach Jira", ex.Message);
        }
    }

    private async Task<TicketCreationResult> UpdateExistingTicketAsync(ActionItem actionItem)
    {
        if (!IsJiraConfigured)
        {
            return Simulate(actionItem, TicketOperation.Updated);
        }

        try
        {
            var payload = new
            {
                body = new
                {
                    type = "doc",
                    version = 1,
                    content = new[]
                    {
                        new { type = "paragraph", content = new[] { new { type = "text", text = CreateUpdateComment(actionItem) } } }
                    }
                }
            };

            using var client = CreateClient();
            using var response = await client.PostAsync(
                $"{_jiraBaseUrl}/rest/api/3/issue/{Uri.EscapeDataString(actionItem.ExistingTicketKey!)}/comment",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Commented on Jira ticket {TicketKey}", actionItem.ExistingTicketKey);
                return new TicketCreationResult
                {
                    Success = true,
                    TicketKey = actionItem.ExistingTicketKey,
                    TicketUrl = $"{_jiraBaseUrl}/browse/{actionItem.ExistingTicketKey}",
                    Operation = TicketOperation.Updated,
                    Message = $"Successfully updated ticket {actionItem.ExistingTicketKey}",
                    ActionItemId = actionItem.Id
                };
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Jira refused the comment on {TicketKey}: {Status} {Body}",
                actionItem.ExistingTicketKey, (int)response.StatusCode, body);
            return Failed(actionItem, TicketOperation.Updated, $"Jira answered {(int)response.StatusCode}", body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Jira ticket {TicketKey}", actionItem.ExistingTicketKey);
            return Failed(actionItem, TicketOperation.Updated, "Could not reach Jira", ex.Message);
        }
    }

    private static TicketCreationResult Failed(ActionItem actionItem, TicketOperation operation, string message, string error) =>
        new()
        {
            Success = false,
            Operation = operation,
            TicketKey = actionItem.ExistingTicketKey,
            Message = message,
            ErrorMessage = error,
            ActionItemId = actionItem.Id
        };

    /// <summary>
    /// What would be created or updated when Jira isn't configured
    /// </summary>
    private TicketCreationResult Simulate(ActionItem actionItem, TicketOperation operation, MeetingTranscript? transcript = null)
    {
        var ticketKey = operation == TicketOperation.Updated && !string.IsNullOrEmpty(actionItem.ExistingTicketKey)
            ? actionItem.ExistingTicketKey
            : $"{actionItem.ProjectKey ?? _defaultProjectKey}-{Random.Shared.Next(1000, 9999)}";
        var message = operation == TicketOperation.Updated
            ? $"Would update existing ticket: {ticketKey}"
            : $"Would create new ticket: {ticketKey}";
        var ticketUrl = $"[Simulated] {_jiraBaseUrl}/browse/{ticketKey}";

        if (transcript != null && operation == TicketOperation.Created)
        {
            transcript.CreatedJiraTickets.Add(new JiraTicketReference
            {
                TicketKey = ticketKey,
                TicketUrl = ticketUrl,
                Title = actionItem.Title,
                ActionItemId = actionItem.Id,
                CreatedAt = DateTime.UtcNow,
                Priority = actionItem.Priority.ToString(),
                Type = actionItem.Type.ToString(),
                AssignedTo = actionItem.AssignedTo,
                Status = "Open"
            });
        }

        _logger.LogInformation("{Message}: {Title} ({Type}, {Priority})", message, actionItem.Title, actionItem.Type, actionItem.Priority);
        return new TicketCreationResult
        {
            Success = true,
            TicketKey = ticketKey,
            TicketUrl = ticketUrl,
            Operation = operation,
            Message = message,
            ActionItemId = actionItem.Id
        };
    }

    /// <summary>
    /// A Jira description in Atlassian Document Format (ADF)
    /// </summary>
    private static object CreateJiraDescriptionADF(string description, MeetingTranscript transcript)
    {
        static object Strong(string text) => new { type = "text", text, marks = new object[] { new { type = "strong" } } };
        static object Plain(string text) => new { type = "text", text };
        static object Paragraph(params object[] content) => new { type = "paragraph", content };

        var content = new List<object>
        {
            Paragraph(Strong("Generated from Meeting Transcript")),
            Paragraph(Strong("Meeting: "), Plain(transcript.Title)),
            Paragraph(Strong("Date: "), Plain(transcript.MeetingDate.ToString("yyyy-MM-dd"))),
            Paragraph(Strong("Transcript File: "), Plain(transcript.FileName)),
            Paragraph(Strong("Description:")),
            Paragraph(Plain(description))
        };

        if (transcript.Participants.Count > 0)
        {
            content.Add(Paragraph(Strong("Meeting Participants: "), Plain(string.Join(", ", transcript.Participants))));
        }

        return new { type = "doc", version = 1, content };
    }

    private static string CreateUpdateComment(ActionItem actionItem)
    {
        var comment = new StringBuilder();
        comment.AppendLine("🤖 *Meeting Transcript Update*");
        comment.AppendLine();
        comment.AppendLine($"*Action Item:* {actionItem.Title}");
        comment.AppendLine($"*Extracted:* {actionItem.ExtractedAt:yyyy-MM-dd HH:mm}");

        if (!string.IsNullOrEmpty(actionItem.Description))
        {
            comment.AppendLine();
            comment.AppendLine($"*Details:* {actionItem.Description}");
        }

        if (!string.IsNullOrEmpty(actionItem.Context))
        {
            comment.AppendLine();
            comment.AppendLine($"*Context:* {actionItem.Context}");
        }

        return comment.ToString();
    }

    private static string MapActionItemTypeToJiraIssueType(ActionItemType type) => type switch
    {
        ActionItemType.Bug => "Bug",
        ActionItemType.Story => "Story",
        ActionItemType.Epic => "Epic",
        _ => "Task"
    };

    private static string[] CreateLabels(ActionItem actionItem, MeetingTranscript transcript) =>
    [
        "meeting-transcript",
        "ai-generated",
        $"type-{actionItem.Type.ToString().ToLowerInvariant()}",
        .. actionItem.Labels,
        $"meeting-{transcript.MeetingDate:yyyy-MM-dd}"
    ];

    /// <summary>
    /// True when a title still looks like dialogue or JSON rather than an action
    /// </summary>
    private static bool IsConversationSnippet(string title) =>
        !string.IsNullOrEmpty(title)
        && ConversationPatterns.Any(pattern => Regex.IsMatch(title, pattern, RegexOptions.IgnoreCase));

    /// <summary>
    /// Asks the model for a clean title when the formatted one still looks like dialogue
    /// </summary>
    private async Task<FormattedTicket> CleanupTitleWithAIAsync(FormattedTicket ticket, ActionItem actionItem)
    {
        try
        {
            var prompt = $@"
URGENT TITLE CLEANUP TASK:

You have a malformed JIRA ticket title that contains conversation snippets. Your job is to extract a clean, actionable title.

MALFORMED TITLE: {ticket.Title}
DESCRIPTION: {ticket.Description}
ACTION ITEM CONTEXT: {actionItem.Context}

TASK: Create a clean, actionable JIRA ticket title that:
1. Starts with an action verb (Create, Fix, Update, Investigate, Review, etc.)
2. Is under 60 characters
3. Describes WHAT needs to be done (not WHO said it)
4. Is professional and clear

EXAMPLES:
- If conversation mentions 'I'll create a ticket for API bug' → 'Fix API authentication bug'
- If conversation mentions 'We need to update documentation' → 'Update API documentation'
- If conversation mentions 'Someone should investigate the issue' → 'Investigate database performance issue'

Respond with ONLY the clean title, nothing else. No JSON, no quotes, just the clean title.
";

            var answer = await _aiService.ProcessTranscriptAsync(prompt);
            var title = answer.Content.Trim().Trim('"').Trim();
            ticket.Title = string.IsNullOrEmpty(title) || title.Length > 80 ? "Complete meeting action item" : title;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Title cleanup with the model failed; using rules");
            ticket.Title = ExtractActionFromConversation(ticket.Title, ticket.Description);
        }

        return ticket;
    }

    private static string ExtractActionFromConversation(string conversationTitle, string description)
    {
        var text = conversationTitle + " " + description;
        foreach (var pattern in ActionPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups[1].Value.Trim().Length > 3)
            {
                var action = match.Groups[1].Value.Replace("a ticket", "").Replace("the ticket", "").Trim();
                if (action.Length > 0)
                {
                    return char.ToUpper(action[0]) + action[1..];
                }
            }
        }

        return "Complete meeting action item";
    }

    /// <summary>
    /// Asks the model for a clean, English title and description; the action item's own are kept when that fails
    /// </summary>
    private async Task<FormattedTicket> FormatTicketWithAIAsync(ActionItem actionItem, MeetingTranscript transcript)
    {
        try
        {
            var formatted = await _aiService.FormatJiraTicketAsync(
                actionItem.Title,
                actionItem.Description,
                $"Meeting: {transcript.Title}, Date: {transcript.MeetingDate:yyyy-MM-dd}",
                string.Join(", ", transcript.Participants));
            var data = JsonSerializer.Deserialize<JsonElement>(AiResponseParser.WithoutCodeFence(formatted));

            string? Text(string name) =>
                data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            return new FormattedTicket
            {
                Title = Text("title") ?? actionItem.Title,
                Description = Text("description") ?? actionItem.Description,
                Priority = Enum.TryParse<ActionItemPriority>(Text("priority"), true, out var priority) ? priority : ActionItemPriority.Medium,
                Type = Enum.TryParse<ActionItemType>(Text("type"), true, out var type) ? type : ActionItemType.Task,
                Labels = data.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array
                    ? labels.EnumerateArray().Select(label => label.GetString()).OfType<string>().ToArray()
                    : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Formatting the ticket with the model failed; using the action item as it is");
            return new FormattedTicket
            {
                Title = actionItem.Title,
                Description = actionItem.Description,
                Priority = actionItem.Priority,
                Type = actionItem.Type
            };
        }
    }
}
