using System.Text;
using System.Text.Json;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Service for processing transcript content using Azure OpenAI
/// </summary>
public class AzureOpenAIService : IAzureOpenAIService, IDisposable
{
    private readonly IConfigurationService _configService;
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;

    public AzureOpenAIService(IConfigurationService configService, ILogger? logger = null)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger;
        _httpClient = new HttpClient();

        // Configure HttpClient for Azure OpenAI API if credentials are available
        if (IsConfigured())
        {
            var settings = _configService.GetAzureOpenAISettings();
            _httpClient.DefaultRequestHeaders.Add("api-key", settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
            );
        }
    }

    /// <summary>
    /// Processes transcript content using Azure OpenAI to extract action items
    /// </summary>
    public async Task<string> ProcessTranscriptAsync(string prompt)
    {
        if (!IsConfigured())
        {
            _logger?.LogWarning("Azure OpenAI not configured, using fallback processing");
            return await ProcessWithFallbackAsync(prompt);
        }

        try
        {
            Console.WriteLine("🤖 Calling Azure OpenAI for transcript analysis...");

            var settings = _configService.GetAzureOpenAISettings();

            // Merge system prompt with custom prompt if available
            var systemPrompt = settings.SystemPrompt;
            if (!string.IsNullOrWhiteSpace(settings.CustomPrompt))
            {
                systemPrompt = $"{settings.SystemPrompt}\n\nAdditional guidance based on training feedback:\n{settings.CustomPrompt}";
                Console.WriteLine("📝 Using merged system + custom prompt");
            }

            var requestBody = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt
                    },
                    new { role = "user", content = prompt }
                },
                max_tokens = settings.MaxTokens,
                temperature = settings.Temperature,
                top_p = settings.TopP
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{settings.Endpoint}/openai/deployments/{settings.DeploymentName}/chat/completions?api-version={settings.ApiVersion}";
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (
                    responseData.TryGetProperty("choices", out var choices)
                    && choices.GetArrayLength() > 0
                )
                {
                    var firstChoice = choices[0];
                    if (
                        firstChoice.TryGetProperty("message", out var message)
                        && message.TryGetProperty("content", out var messageContent)
                    )
                    {
                        var result = messageContent.GetString() ?? "";
                        Console.WriteLine("✅ Azure OpenAI analysis completed");
                        return result;
                    }
                }

                throw new InvalidOperationException("Unexpected response format from Azure OpenAI");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError(
                    new Exception($"Azure OpenAI API error: {response.StatusCode}"),
                    errorContent
                );
                Console.WriteLine($"❌ Azure OpenAI API error: {response.StatusCode}");

                // Fallback to local processing
                return await ProcessWithFallbackAsync(prompt);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calling Azure OpenAI");
            Console.WriteLine($"❌ Azure OpenAI error: {ex.Message}");

            // Fallback to local processing
            return await ProcessWithFallbackAsync(prompt);
        }
    }

    /// <summary>
    /// Formats action items into properly structured Jira ticket data using Azure OpenAI
    /// </summary>
    public async Task<string> FormatJiraTicketAsync(
        string actionItemTitle,
        string actionItemDescription,
        string meetingContext,
        string participants
    )
    {
        if (!IsConfigured())
        {
            _logger?.LogWarning("Azure OpenAI not configured, using fallback formatting");
            return await FormatJiraTicketFallbackAsync(
                actionItemTitle,
                actionItemDescription,
                meetingContext
            );
        }

        try
        {
            Console.WriteLine($"🎫 Formatting Jira ticket for: {actionItemTitle}");

            var prompt =
                $@"
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

            var settings = _configService.GetAzureOpenAISettings();
            var requestBody = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an expert at creating well-formatted Jira tickets in ENGLISH ONLY. Regardless of the input language, you must always translate and create clear, actionable ticket titles and descriptions in English. You always respond with valid JSON format."
                    },
                    new { role = "user", content = prompt }
                },
                max_tokens = 1500,
                temperature = 0.2, // Low temperature for consistent formatting
                top_p = 0.9
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{settings.Endpoint}/openai/deployments/{settings.DeploymentName}/chat/completions?api-version={settings.ApiVersion}";
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (
                    responseData.TryGetProperty("choices", out var choices)
                    && choices.GetArrayLength() > 0
                )
                {
                    var firstChoice = choices[0];
                    if (
                        firstChoice.TryGetProperty("message", out var message)
                        && message.TryGetProperty("content", out var messageContent)
                    )
                    {
                        var result = messageContent.GetString() ?? "";
                        Console.WriteLine("✅ Jira ticket formatting completed");
                        return result;
                    }
                }

                throw new InvalidOperationException("Unexpected response format from Azure OpenAI");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError(
                    new Exception($"Azure OpenAI API error: {response.StatusCode}"),
                    errorContent
                );
                return await FormatJiraTicketFallbackAsync(
                    actionItemTitle,
                    actionItemDescription,
                    meetingContext
                );
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calling Azure OpenAI for Jira ticket formatting");
            Console.WriteLine($"❌ Azure OpenAI formatting failed: {ex.Message}");
            return await FormatJiraTicketFallbackAsync(
                actionItemTitle,
                actionItemDescription,
                meetingContext
            );
        }
    }

    /// <summary>
    /// Checks if Azure OpenAI is properly configured
    /// </summary>
    public bool IsConfigured()
    {
        var settings = _configService.GetAzureOpenAISettings();

        // Debug output to help identify the issue
        Console.WriteLine("🔍 Azure OpenAI Configuration Debug:");
        Console.WriteLine($"   Endpoint: '{settings.Endpoint}' (Empty: {string.IsNullOrEmpty(settings.Endpoint)})");
        Console.WriteLine($"   ApiKey: '{settings.ApiKey?.Substring(0, Math.Min(10, settings.ApiKey?.Length ?? 0))}...' (Empty: {string.IsNullOrEmpty(settings.ApiKey)})");
        Console.WriteLine($"   DeploymentName: '{settings.DeploymentName}' (Empty: {string.IsNullOrEmpty(settings.DeploymentName)})");

        var isConfigured = !string.IsNullOrEmpty(settings.Endpoint)
            && !string.IsNullOrEmpty(settings.ApiKey)
            && !string.IsNullOrEmpty(settings.DeploymentName);

        Console.WriteLine($"   IsConfigured Result: {isConfigured}");

        return isConfigured;
    }

    /// <summary>
    /// Fallback processing when Azure OpenAI is not available
    /// </summary>
    private async Task<string> ProcessWithFallbackAsync(string prompt)
    {
        await Task.Delay(100); // Simulate processing time

        Console.WriteLine("⚙️ Using rule-based fallback processing...");

        var lines = prompt.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var actionItems = new List<object>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Look for action item indicators
            if (ContainsActionItemKeywords(trimmedLine) && trimmedLine.Length > 10)
            {
                var actionItem = new
                {
                    title = ExtractTitle(trimmedLine),
                    description = trimmedLine,
                    assignedTo = ExtractAssignee(trimmedLine),
                    dueDate = ExtractDueDate(trimmedLine),
                    priority = DeterminePriority(trimmedLine),
                    type = DetermineType(trimmedLine),
                    context = trimmedLine,
                    requiresJiraTicket = true
                };

                actionItems.Add(actionItem);
            }
        }

        var result = new { actionItems };
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Fallback method for formatting Jira tickets when AI is not available
    /// </summary>
    private async Task<string> FormatJiraTicketFallbackAsync(
        string title,
        string description,
        string context
    )
    {
        await Task.Delay(50); // Simulate processing time

        // Clean up the title
        var cleanTitle = title
            .Replace("Create new Jira ticket:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Create Jira ticket:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Action item:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("TODO:", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        // Check if title contains conversation snippets and extract action
        if (cleanTitle.Contains("\"context\":") || cleanTitle.Contains("context\":"))
        {
            // Extract action from conversation snippet
            cleanTitle = ExtractActionFromConversation(cleanTitle, description);
        }

        // If title is still empty or too long, use a fallback
        if (string.IsNullOrEmpty(cleanTitle))
        {
            cleanTitle = "Review meeting action item";
        }
        else if (cleanTitle.Length > 80)
        {
            cleanTitle = cleanTitle.Substring(0, 77) + "...";
        }

        var formattedTicket = new
        {
            title = cleanTitle,
            description = !string.IsNullOrEmpty(description) ? description : cleanTitle,
            priority = "Medium",
            type = "Task",
            labels = new[] { "meeting-generated", "action-item" }
        };

        return JsonSerializer.Serialize(
            formattedTicket,
            new JsonSerializerOptions { WriteIndented = true }
        );
    }

    /// <summary>
    /// Extracts actionable title from conversation snippets
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

        foreach (var pattern in actionPatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                conversationTitle,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (match.Success && match.Groups[1].Value.Trim().Length > 3)
            {
                var action = match.Groups[1].Value.Trim();
                return char.ToUpper(action[0]) + action.Substring(1);
            }
        }

        // If no action found, try to extract from description
        if (!string.IsNullOrEmpty(description))
        {
            foreach (var pattern in actionPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    description,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                if (match.Success && match.Groups[1].Value.Trim().Length > 3)
                {
                    var action = match.Groups[1].Value.Trim();
                    return char.ToUpper(action[0]) + action.Substring(1);
                }
            }
        }

        // Default fallback
        return "Complete meeting action item";
    }

    #region Helper Methods

    // These values are hard-coded for simplicity, but in reality they would be in a configuration file
    private static bool ContainsActionItemKeywords(string line)
    {
        var keywords = new[]
        {
            "action",
            "todo",
            "task",
            "implement",
            "create",
            "fix",
            "review",
            "update",
            "investigate"
        };
        return keywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractTitle(string line)
    {
        // Clean up the line to extract a proper title
        var title = line.Replace("Action item:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("TODO:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Create new Jira ticket:", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return string.IsNullOrEmpty(title) ? "Action item" : title;
    }

    private static string? ExtractAssignee(string line)
    {
        var patterns = new[] { @"assigned\s+to:?\s*([^\s,]+)", @"@([^\s,]+)" };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                line,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    private static string? ExtractDueDate(string line)
    {
        var patterns = new[] { @"due:?\s*(\d{4}-\d{2}-\d{2})", @"by\s+(\d{1,2}/\d{1,2}/\d{4})" };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                line,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    private static string DeterminePriority(string line)
    {
        var lowerLine = line.ToLowerInvariant();

        if (
            lowerLine.Contains("urgent")
            || lowerLine.Contains("critical")
            || lowerLine.Contains("high")
        )
            return "High";

        if (lowerLine.Contains("low") || lowerLine.Contains("minor"))
            return "Low";

        return "Medium";
    }

    private static string DetermineType(string line)
    {
        var lowerLine = line.ToLowerInvariant();

        if (lowerLine.Contains("bug") || lowerLine.Contains("fix"))
            return "Bug";

        if (lowerLine.Contains("investigate") || lowerLine.Contains("research"))
            return "Investigation";

        if (lowerLine.Contains("document") || lowerLine.Contains("doc"))
            return "Documentation";

        if (lowerLine.Contains("review"))
            return "Review";

        return "Task";
    }

    #endregion

    /// <summary>
    /// Disposes of the HttpClient resources
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
