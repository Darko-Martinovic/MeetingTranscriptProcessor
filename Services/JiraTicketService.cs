using MeetingTranscriptProcessor.Models;
using System.Text;
using System.Text.Json;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Service for creating and updating Jira tickets from action items
/// </summary>
public class JiraTicketService : IJiraTicketService, IDisposable
{
    private readonly string? _jiraBaseUrl;
    private readonly string? _jiraApiToken;
    private readonly string? _jiraUserEmail;
    private readonly string? _defaultProjectKey;
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;
    private readonly IAzureOpenAIService _aiService;

    public JiraTicketService(IAzureOpenAIService aiService, ILogger? logger = null)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));

        // Load Jira configuration from environment variables
        _jiraBaseUrl = Environment.GetEnvironmentVariable("JIRA_URL");
        _jiraApiToken = Environment.GetEnvironmentVariable("JIRA_API_TOKEN");
        _jiraUserEmail = Environment.GetEnvironmentVariable("JIRA_EMAIL");
        _defaultProjectKey =
            Environment.GetEnvironmentVariable("JIRA_PROJECT_KEY")
            ?? Environment.GetEnvironmentVariable("JIRA_DEFAULT_PROJECT")
            ?? "TASK";
        _logger = logger;

        // Debug output for JIRA configuration
        Console.WriteLine("🔍 JIRA Configuration Debug:");
        Console.WriteLine($"   JIRA_URL: {(!string.IsNullOrWhiteSpace(_jiraBaseUrl) ? "✓ Set" : "❌ Missing")} = '{_jiraBaseUrl}'");
        Console.WriteLine($"   JIRA_API_TOKEN: {(!string.IsNullOrWhiteSpace(_jiraApiToken) ? "✓ Set" : "❌ Missing")} = '{(_jiraApiToken?.Length > 10 ? _jiraApiToken[..10] + "..." : _jiraApiToken)}'");
        Console.WriteLine($"   JIRA_EMAIL: {(!string.IsNullOrWhiteSpace(_jiraUserEmail) ? "✓ Set" : "❌ Missing")} = '{_jiraUserEmail}'");
        Console.WriteLine($"   JIRA_PROJECT_KEY: {_defaultProjectKey} (env: '{Environment.GetEnvironmentVariable("JIRA_PROJECT_KEY")}')");
        Console.WriteLine($"   IsJiraConfigured: {IsJiraConfigured()}");

        _httpClient = new HttpClient();

        // Configure HttpClient for Jira API if credentials are available
        if (IsJiraConfigured())
        {
            var authString = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_jiraUserEmail}:{_jiraApiToken}")
            );
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
            );
            Console.WriteLine("✅ JIRA HTTP client configured for real API calls");
        }
        else
        {
            Console.WriteLine("⚠️  JIRA not configured - will use simulation mode");
        }
    }

    /// <summary>
    /// Processes action items and creates/updates Jira tickets
    /// </summary>
    public async Task<TranscriptProcessingResult> ProcessActionItemsAsync(
        MeetingTranscript transcript
    )
    {
        var result = new TranscriptProcessingResult
        {
            TranscriptId = transcript.Id,
            FileName = transcript.FileName,
            ActionItemsFound = transcript.ActionItems.Count
        };

        var startTime = DateTime.UtcNow;

        try
        {
            Console.WriteLine(
                $"🎫 Processing {transcript.ActionItems.Count} action items for Jira tickets"
            );

            if (!transcript.ActionItems.Any())
            {
                result.Success = true;
                result.ProcessingDuration = DateTime.UtcNow - startTime;
                Console.WriteLine("ℹ️  No action items found - nothing to process");
                return result;
            }

            foreach (var actionItem in transcript.ActionItems)
            {
                if (!actionItem.RequiresJiraTicket)
                {
                    var skippedResult = new TicketCreationResult
                    {
                        Success = true,
                        Operation = TicketOperation.Skipped,
                        Message = "Action item marked as not requiring Jira ticket",
                        ActionItemId = actionItem.Id
                    };
                    result.TicketResults.Add(skippedResult);
                    continue;
                }

                TicketCreationResult ticketResult;

                if (!string.IsNullOrEmpty(actionItem.ExistingTicketKey))
                {
                    // Update existing ticket
                    ticketResult = await UpdateExistingTicketAsync(actionItem);
                    if (ticketResult.Success)
                        result.TicketsUpdated++;
                }
                else
                {
                    // Create new ticket
                    ticketResult = await CreateNewTicketAsync(actionItem, transcript);
                    if (ticketResult.Success)
                        result.TicketsCreated++;
                }

                result.TicketResults.Add(ticketResult);
            }

            result.Success = result.TicketResults.Any(r => r.Success);
            result.ProcessingDuration = DateTime.UtcNow - startTime;

            Console.WriteLine(
                $"✅ Processed {result.TicketsCreated} created, {result.TicketsUpdated} updated tickets"
            );

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.ProcessingDuration = DateTime.UtcNow - startTime;

            _logger?.LogError(ex, "Error processing action items");
            Console.WriteLine($"❌ Error processing action items: {ex.Message}");

            return result;
        }
    }

    /// <summary>
    /// Creates a new Jira ticket from an action item
    /// </summary>
    private async Task<TicketCreationResult> CreateNewTicketAsync(
        ActionItem actionItem,
        MeetingTranscript transcript
    )
    {
        try
        {
            if (!IsJiraConfigured())
            {
                return await SimulateTicketCreationAsync(actionItem, TicketOperation.Created, transcript);
            }

            // Use AI to format the ticket properly
            var formattedTicket = await FormatTicketWithAIAsync(actionItem, transcript);

            // Check if the title still contains conversation snippets and needs additional cleaning
            if (IsConversationSnippet(formattedTicket.Title))
            {
                Console.WriteLine($"🧹 Title contains conversation snippet: '{formattedTicket.Title}'");
                Console.WriteLine($"🧹 Attempting AI cleanup...");
                formattedTicket = await CleanupTitleWithAIAsync(formattedTicket, actionItem, transcript);
            }
            else
            {
                Console.WriteLine($"✅ Title looks good: '{formattedTicket.Title}'");
            }

            var projectKey = actionItem.ProjectKey ?? transcript.ProjectKey ?? _defaultProjectKey;

            var issuePayload = new
            {
                fields = new
                {
                    project = new { key = projectKey },
                    summary = formattedTicket.Title,
                    description = CreateJiraDescriptionADF(formattedTicket.Description, transcript),
                    issuetype = new
                    {
                        name = MapActionItemTypeToJiraIssueType(formattedTicket.Type) // Use proper mapping
                    },
                    // priority = new { name = MapPriorityToJira(formattedTicket.Priority) }, // Commented out - field not on screen
                    assignee = string.IsNullOrEmpty(actionItem.AssignedTo)
                        ? null
                        : new { name = actionItem.AssignedTo },
                    duedate = actionItem.DueDate?.ToString("yyyy-MM-dd"),
                    labels = formattedTicket.Labels ?? CreateLabels(actionItem, transcript)
                }
            };

            var json = JsonSerializer.Serialize(
                issuePayload,
                new JsonSerializerOptions { WriteIndented = true }
            );
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{_jiraBaseUrl}/rest/api/3/issue";

            Console.WriteLine("═══════ JIRA TICKET CREATION DEBUG ═══════");
            Console.WriteLine($"🔍 Making JIRA API call:");
            Console.WriteLine($"   URL: {url}");
            Console.WriteLine($"   Project Key: {projectKey}");
            Console.WriteLine($"   Action Item ProjectKey: {actionItem.ProjectKey}");
            Console.WriteLine($"   Transcript ProjectKey: {transcript.ProjectKey}");
            Console.WriteLine($"   Default ProjectKey: {_defaultProjectKey}");
            Console.WriteLine($"   Title: {formattedTicket.Title}");
            Console.WriteLine("═══════════════════════════════════════════");

            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (responseData.TryGetProperty("key", out var keyElement))
                {
                    var ticketKey = keyElement.GetString();
                    var ticketUrl = $"{_jiraBaseUrl}/browse/{ticketKey}";

                    Console.WriteLine($"✅ Created Jira ticket: {ticketKey}");

                    // Add ticket reference to transcript
                    var ticketReference = new JiraTicketReference
                    {
                        TicketKey = ticketKey ?? "",
                        TicketUrl = ticketUrl,
                        Title = formattedTicket.Title,
                        ActionItemId = actionItem.Id,
                        CreatedAt = DateTime.UtcNow,
                        Priority = formattedTicket.Priority.ToString(),
                        Type = formattedTicket.Type.ToString(),
                        AssignedTo = actionItem.AssignedTo,
                        Status = "Open"
                    };

                    transcript.CreatedJiraTickets.Add(ticketReference);
                    Console.WriteLine($"📎 Added ticket reference to meeting: {ticketKey}");

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
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger?.LogError(
                new Exception($"Jira API error: {response.StatusCode}"),
                errorContent
            );

            Console.WriteLine($"❌ JIRA API Error: {response.StatusCode}");
            Console.WriteLine($"   Response: {errorContent}");
            Console.WriteLine("   Falling back to simulation mode");

            // Fallback to simulation
            return await SimulateTicketCreationAsync(actionItem, TicketOperation.Created, transcript);
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                $"Error creating Jira ticket for action item: {actionItem.Title}"
            );
            Console.WriteLine($"❌ Exception creating JIRA ticket: {ex.Message}");
            Console.WriteLine("   Falling back to simulation mode");
            return await SimulateTicketCreationAsync(actionItem, TicketOperation.Created, transcript);
        }
    }

    /// <summary>
    /// Updates an existing Jira ticket with action item information
    /// </summary>
    private async Task<TicketCreationResult> UpdateExistingTicketAsync(ActionItem actionItem)
    {
        try
        {
            if (!IsJiraConfigured())
            {
                return await SimulateTicketCreationAsync(actionItem, TicketOperation.Updated);
            }

            // Add comment to existing ticket
            var comment = CreateUpdateComment(actionItem);
            var commentPayload = new
            {
                body = new
                {
                    type = "doc",
                    version = 1,
                    content = new[]
                    {
                        new
                        {
                            type = "paragraph",
                            content = new[] { new { type = "text", text = comment } }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(commentPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{_jiraBaseUrl}/rest/api/3/issue/{actionItem.ExistingTicketKey}/comment";
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var ticketUrl = $"{_jiraBaseUrl}/browse/{actionItem.ExistingTicketKey}";

                Console.WriteLine($"✅ Updated Jira ticket: {actionItem.ExistingTicketKey}");

                return new TicketCreationResult
                {
                    Success = true,
                    TicketKey = actionItem.ExistingTicketKey,
                    TicketUrl = ticketUrl,
                    Operation = TicketOperation.Updated,
                    Message = $"Successfully updated ticket {actionItem.ExistingTicketKey}",
                    ActionItemId = actionItem.Id
                };
            }

            // Fallback to simulation
            return await SimulateTicketCreationAsync(actionItem, TicketOperation.Updated);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Error updating Jira ticket: {actionItem.ExistingTicketKey}");
            return await SimulateTicketCreationAsync(actionItem, TicketOperation.Updated);
        }
    }

    /// <summary>
    /// Simulates ticket creation/update when Jira is not configured
    /// </summary>
    private async Task<TicketCreationResult> SimulateTicketCreationAsync(
        ActionItem actionItem,
        TicketOperation operation,
        MeetingTranscript? transcript = null
    )
    {
        await Task.Delay(100); // Simulate API call time

        var ticketKey =
            operation == TicketOperation.Updated
            && !string.IsNullOrEmpty(actionItem.ExistingTicketKey)
                ? actionItem.ExistingTicketKey
                : GenerateSimulatedTicketKey(actionItem);

        var message = operation switch
        {
            TicketOperation.Created => $"Would create new ticket: {ticketKey}",
            TicketOperation.Updated => $"Would update existing ticket: {ticketKey}",
            _ => $"Would process ticket: {ticketKey}"
        };

        Console.WriteLine($"🎫 {message}");
        Console.WriteLine($"   📋 Title: {actionItem.Title}");
        Console.WriteLine($"   🔹 Type: {actionItem.Type}");
        Console.WriteLine($"   ⭐ Priority: {actionItem.Priority}");

        if (!string.IsNullOrEmpty(actionItem.AssignedTo))
        {
            Console.WriteLine($"   👤 Assigned: {actionItem.AssignedTo}");
        }

        if (actionItem.DueDate.HasValue)
        {
            Console.WriteLine($"   📅 Due: {actionItem.DueDate.Value:yyyy-MM-dd}");
        }

        // Add ticket reference to transcript even in simulation mode
        if (transcript != null && operation == TicketOperation.Created)
        {
            var ticketReference = new JiraTicketReference
            {
                TicketKey = ticketKey ?? "",
                TicketUrl = $"[Simulated] {_jiraBaseUrl}/browse/{ticketKey}",
                Title = actionItem.Title,
                ActionItemId = actionItem.Id,
                CreatedAt = DateTime.UtcNow,
                Priority = actionItem.Priority.ToString(),
                Type = actionItem.Type.ToString(),
                AssignedTo = actionItem.AssignedTo,
                Status = "Open"
            };

            transcript.CreatedJiraTickets.Add(ticketReference);
            Console.WriteLine($"📎 Added simulated ticket reference to meeting: {ticketKey}");
        }

        return new TicketCreationResult
        {
            Success = true,
            TicketKey = ticketKey,
            TicketUrl = $"[Simulated] {_jiraBaseUrl}/browse/{ticketKey}",
            Operation = operation,
            Message = message,
            ActionItemId = actionItem.Id
        };
    }

    /// <summary>
    /// Creates a Jira description from action item
    /// </summary>
    /// <summary>
    /// Creates a Jira description in Atlassian Document Format (ADF)
    /// </summary>
    private static object CreateJiraDescriptionADF(string description, MeetingTranscript transcript)
    {
        var content = new List<object>();

        // Header
        content.Add(
            new
            {
                type = "paragraph",
                content = new object[]
                {
                    new
                    {
                        type = "text",
                        text = "Generated from Meeting Transcript",
                        marks = new object[] { new { type = "strong" } }
                    }
                }
            }
        );

        // Meeting details
        content.Add(
            new
            {
                type = "paragraph",
                content = new object[]
                {
                    new
                    {
                        type = "text",
                        text = "Meeting: ",
                        marks = new object[] { new { type = "strong" } }
                    },
                    new { type = "text", text = transcript.Title }
                }
            }
        );

        content.Add(
            new
            {
                type = "paragraph",
                content = new object[]
                {
                    new
                    {
                        type = "text",
                        text = "Date: ",
                        marks = new object[] { new { type = "strong" } }
                    },
                    new { type = "text", text = transcript.MeetingDate.ToString("yyyy-MM-dd") }
                }
            }
        );

        content.Add(
            new
            {
                type = "paragraph",
                content = new object[]
                {
                    new
                    {
                        type = "text",
                        text = "Transcript File: ",
                        marks = new object[] { new { type = "strong" } }
                    },
                    new { type = "text", text = transcript.FileName }
                }
            }
        );

        // Description
        content.Add(
            new
            {
                type = "paragraph",
                content = new object[]
                {
                    new
                    {
                        type = "text",
                        text = "Description:",
                        marks = new object[] { new { type = "strong" } }
                    }
                }
            }
        );

        content.Add(
            new
            {
                type = "paragraph",
                content = new object[] { new { type = "text", text = description } }
            }
        );

        // Participants if available
        if (transcript.Participants.Any())
        {
            content.Add(
                new
                {
                    type = "paragraph",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = "Meeting Participants: ",
                            marks = new object[] { new { type = "strong" } }
                        },
                        new { type = "text", text = string.Join(", ", transcript.Participants) }
                    }
                }
            );
        }

        return new
        {
            type = "doc",
            version = 1,
            content = content
        };
    }

    /// <summary>
    /// Creates a Jira description in plain text format (legacy method)
    /// </summary>
    private static string CreateJiraDescription(ActionItem actionItem, MeetingTranscript transcript)
    {
        var description = new StringBuilder();

        description.AppendLine("*Generated from Meeting Transcript*");
        description.AppendLine();
        description.AppendLine($"*Meeting:* {transcript.Title}");
        description.AppendLine($"*Date:* {transcript.MeetingDate:yyyy-MM-dd}");
        description.AppendLine($"*Transcript File:* {transcript.FileName}");
        description.AppendLine();
        description.AppendLine("*Description:*");
        description.AppendLine(actionItem.Description);

        if (!string.IsNullOrEmpty(actionItem.Context))
        {
            description.AppendLine();
            description.AppendLine("*Original Context:*");
            description.AppendLine($"{{quote}}{actionItem.Context}{{quote}}");
        }

        if (transcript.Participants.Any())
        {
            description.AppendLine();
            description.AppendLine(
                $"*Meeting Participants:* {string.Join(", ", transcript.Participants)}"
            );
        }

        return description.ToString();
    }

    /// <summary>
    /// Creates an update comment for existing tickets
    /// </summary>
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

    /// <summary>
    /// Maps action item type to Jira issue type
    /// </summary>
    private static string MapActionItemTypeToJiraIssueType(ActionItemType type)
    {
        return type switch
        {
            ActionItemType.Bug => "Bug",
            ActionItemType.Story => "Story",
            ActionItemType.Epic => "Epic",
            ActionItemType.Investigation => "Task",
            ActionItemType.Documentation => "Task",
            ActionItemType.Review => "Task",
            ActionItemType.Task => "Task",
            _ => "Task"
        };
    }

    /// <summary>
    /// Cleans AI JSON response by removing markdown code blocks and other formatting
    /// </summary>
    private static string CleanAIJsonResponse(string aiResponse)
    {
        if (string.IsNullOrWhiteSpace(aiResponse))
            return aiResponse;

        // Remove markdown code blocks
        var cleaned = aiResponse.Trim();

        // Remove leading/trailing backticks and json specifier
        if (cleaned.StartsWith("```json"))
            cleaned = cleaned.Substring(7);
        else if (cleaned.StartsWith("```"))
            cleaned = cleaned.Substring(3);

        if (cleaned.EndsWith("```"))
            cleaned = cleaned.Substring(0, cleaned.Length - 3);

        return cleaned.Trim();
    }

    /// <summary>
    /// Checks if a title contains conversation snippets that need cleaning
    /// </summary>
    private static bool IsConversationSnippet(string title)
    {
        if (string.IsNullOrEmpty(title))
            return false;

        // Check for common conversation snippet patterns
        var conversationPatterns = new[]
        {
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
            @"^\s*\{", // Starts with JSON-like structure
            @"^\s*""[^""]*"":\s*""", // Starts with JSON key-value
            @"Create\s+a?\s+Jira\s+ticket", // Literal "Create Jira ticket" text
            @"Create\s+new\s+Jira\s+ticket" // Literal "Create new Jira ticket" text
        };

        var foundPattern = conversationPatterns.Any(pattern =>
            System.Text.RegularExpressions.Regex.IsMatch(title, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));

        if (foundPattern)
        {
            Console.WriteLine($"🔍 Detected conversation snippet pattern in: '{title}'");
        }

        return foundPattern;
    }

    /// <summary>
    /// Uses AI to clean up malformed titles that contain conversation snippets
    /// </summary>
    private async Task<FormattedTicket> CleanupTitleWithAIAsync(
        FormattedTicket originalTicket,
        ActionItem actionItem,
        MeetingTranscript transcript)
    {
        try
        {
            var cleanupPrompt = $@"
URGENT TITLE CLEANUP TASK:

You have a malformed JIRA ticket title that contains conversation snippets. Your job is to extract a clean, actionable title.

MALFORMED TITLE: {originalTicket.Title}
DESCRIPTION: {originalTicket.Description}
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

            var cleanTitle = await _aiService.ProcessTranscriptAsync(cleanupPrompt);

            // Clean up any extra formatting from AI response
            var titleText = cleanTitle.Content.Trim().Trim('"').Trim();

            // Ensure it's not empty and not too long
            if (string.IsNullOrEmpty(titleText) || titleText.Length > 80)
            {
                titleText = "Complete meeting action item";
            }

            Console.WriteLine($"✅ Title cleaned: '{originalTicket.Title}' → '{titleText}'");

            return new FormattedTicket
            {
                Title = titleText,
                Description = originalTicket.Description,
                Priority = originalTicket.Priority,
                Type = originalTicket.Type,
                Labels = originalTicket.Labels
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error cleaning up title with AI");
            Console.WriteLine($"❌ Title cleanup failed, using fallback");

            // Fallback to simple cleanup
            var fallbackTitle = ExtractActionFromConversation(originalTicket.Title, originalTicket.Description);
            return new FormattedTicket
            {
                Title = fallbackTitle,
                Description = originalTicket.Description,
                Priority = originalTicket.Priority,
                Type = originalTicket.Type,
                Labels = originalTicket.Labels
            };
        }
    }

    /// <summary>
    /// Extracts actionable title from conversation snippets (fallback method)
    /// </summary>
    private static string ExtractActionFromConversation(string conversationTitle, string description)
    {
        // Look for common action patterns in the conversation
        var actionPatterns = new[]
        {
            @"I'll\s+(.*?)(?:\.|,|$)",
            @"I\s+will\s+(.*?)(?:\.|,|$)",
            @"create\s+(.*?)(?:\.|,|$)",
            @"fix\s+(.*?)(?:\.|,|$)",
            @"investigate\s+(.*?)(?:\.|,|$)",
            @"review\s+(.*?)(?:\.|,|$)",
            @"update\s+(.*?)(?:\.|,|$)",
            @"implement\s+(.*?)(?:\.|,|$)"
        };

        var textToSearch = conversationTitle + " " + (description ?? "");

        foreach (var pattern in actionPatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                textToSearch,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (match.Success && match.Groups[1].Value.Trim().Length > 3)
            {
                var action = match.Groups[1].Value.Trim();
                // Clean up common suffixes
                action = action.Replace("a ticket", "").Replace("the ticket", "").Trim();
                return char.ToUpper(action[0]) + action.Substring(1);
            }
        }

        // Default fallback
        return "Complete meeting action item";
    }

    /// <summary>
    /// Maps priority to Jira priority names
    /// </summary>
    private static string MapPriorityToJira(ActionItemPriority priority)
    {
        return priority switch
        {
            ActionItemPriority.Critical => "Highest",
            ActionItemPriority.High => "High",
            ActionItemPriority.Medium => "Medium",
            ActionItemPriority.Low => "Low",
            _ => "Medium"
        };
    }

    /// <summary>
    /// Creates labels for Jira tickets
    /// </summary>
    private static string[] CreateLabels(ActionItem actionItem, MeetingTranscript transcript)
    {
        var labels = new List<string> { "meeting-transcript", "ai-generated" };

        // Add action item type as label
        labels.Add($"type-{actionItem.Type.ToString().ToLowerInvariant()}");

        // Add custom labels from action item
        labels.AddRange(actionItem.Labels);

        // Add meeting date as label
        labels.Add($"meeting-{transcript.MeetingDate:yyyy-MM-dd}");

        return labels.ToArray();
    }

    /// <summary>
    /// Generates a simulated ticket key for demo purposes
    /// </summary>
    private string GenerateSimulatedTicketKey(ActionItem actionItem)
    {
        var projectKey = actionItem.ProjectKey ?? _defaultProjectKey;
        var ticketNumber = new Random().Next(1000, 9999);
        return $"{projectKey}-{ticketNumber}";
    }

    /// <summary>
    /// Checks if Jira is properly configured
    /// </summary>
    private bool IsJiraConfigured()
    {
        return !string.IsNullOrWhiteSpace(_jiraBaseUrl)
            && !string.IsNullOrWhiteSpace(_jiraApiToken)
            && !string.IsNullOrWhiteSpace(_jiraUserEmail);
    }

    /// <summary>
    /// Disposes of the HttpClient resources
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    /// <summary>
    /// Formats a ticket using AI to clean up title and description
    /// </summary>
    private async Task<FormattedTicket> FormatTicketWithAIAsync(
        ActionItem actionItem,
        MeetingTranscript transcript
    )
    {
        try
        {
            var participants = string.Join(", ", transcript.Participants);
            var context = $"Meeting: {transcript.Title}, Date: {transcript.MeetingDate:yyyy-MM-dd}";

            var formattedJson = await _aiService.FormatJiraTicketAsync(
                actionItem.Title,
                actionItem.Description,
                context,
                participants
            );

            // Clean up potential markdown formatting from AI response
            var cleanedJson = CleanAIJsonResponse(formattedJson);

            var formattedData = JsonSerializer.Deserialize<JsonElement>(cleanedJson);

            return new FormattedTicket
            {
                Title = formattedData.TryGetProperty("title", out var titleElement)
                    ? titleElement.GetString() ?? actionItem.Title
                    : actionItem.Title,
                Description = formattedData.TryGetProperty("description", out var descElement)
                    ? descElement.GetString() ?? actionItem.Description
                    : actionItem.Description,
                Priority = Enum.TryParse<ActionItemPriority>(
                    formattedData.TryGetProperty("priority", out var priorityElement)
                        ? priorityElement.GetString() ?? "Medium"
                        : "Medium",
                    out var priority
                )
                    ? priority
                    : ActionItemPriority.Medium,
                Type = Enum.TryParse<ActionItemType>(
                    formattedData.TryGetProperty("type", out var typeElement)
                        ? typeElement.GetString() ?? "Task"
                        : "Task",
                    out var type
                )
                    ? type
                    : ActionItemType.Task,
                Labels =
                    formattedData.TryGetProperty("labels", out var labelsElement)
                    && labelsElement.ValueKind == JsonValueKind.Array
                        ? labelsElement
                            .EnumerateArray()
                            .Select(l => l.GetString())
                            .Where(l => l != null)
                            .Cast<string>()
                            .ToArray()
                        : null
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error formatting ticket with AI, using fallback");

            // Fallback to original action item data
            return new FormattedTicket
            {
                Title = actionItem.Title,
                Description = actionItem.Description,
                Priority = actionItem.Priority,
                Type = actionItem.Type,
                Labels = null
            };
        }
    }
}
