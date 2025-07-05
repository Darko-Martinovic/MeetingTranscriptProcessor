using System.Text;
using System.Text.Json;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Service for processing transcript content using Azure OpenAI
/// </summary>
public class AzureOpenAIService : IDisposable
{
    private readonly string? _endpoint;
    private readonly string? _apiKey;
    private readonly string? _deploymentName;
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;

    public AzureOpenAIService(ILogger? logger = null)
    {
        // Load Azure OpenAI configuration from environment variables
        _endpoint =
            Environment.GetEnvironmentVariable("AOAI_ENDPOINT")
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        _apiKey =
            Environment.GetEnvironmentVariable("AOAI_APIKEY")
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        _deploymentName =
            Environment.GetEnvironmentVariable("CHATCOMPLETION_DEPLOYMENTNAME")
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
            ?? "gpt-4";
        _logger = logger;

        _httpClient = new HttpClient();

        // Configure HttpClient for Azure OpenAI API if credentials are available
        if (IsConfigured())
        {
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
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

            var requestBody = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an expert assistant that analyzes meeting transcripts and extracts actionable items. You respond in valid JSON format."
                    },
                    new { role = "user", content = prompt }
                }, //all these values are hard-coded for simplicity, but in reality it would be in a configuration file
                max_tokens = 4000,
                temperature = 0.1, // Low temperature for consistent, factual responses
                top_p = 0.95
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url =
                $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version=2024-02-15-preview";
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
Format the following action item into a proper Jira ticket with clean title and detailed description.

Action Item Title: {actionItemTitle}
Action Item Description: {actionItemDescription}
Meeting Context: {meetingContext}
Meeting Participants: {participants}

Please provide a response in this exact JSON format:
{{
  ""title"": ""Clean, actionable title without prefixes like 'Create Jira ticket' or 'Action item'"",
  ""description"": ""Detailed description with context and requirements"",
  ""priority"": ""High/Medium/Low"",
  ""type"": ""Task/Bug/Story/Investigation/Documentation/Review"",
  ""labels"": [""meeting-generated"", ""action-item""]
}}

Rules:
1. Title should be concise and actionable (max 80 characters)
2. Remove any prefixes like 'Create new Jira ticket:', 'Action item:', etc.
3. Description should include context from the meeting
4. Choose appropriate priority based on urgency indicators
5. Choose appropriate type based on the action needed
";

            var requestBody = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an expert at creating well-formatted Jira tickets. You always respond with valid JSON and create clear, actionable ticket titles and descriptions."
                    },
                    new { role = "user", content = prompt }
                },
                max_tokens = 1500,
                temperature = 0.2, // Low temperature for consistent formatting
                top_p = 0.9
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url =
                $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version=2024-02-15-preview";
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
        return !string.IsNullOrEmpty(_endpoint)
            && !string.IsNullOrEmpty(_apiKey)
            && !string.IsNullOrEmpty(_deploymentName);
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
