using MeetingTranscriptProcessor.Models;
using System.Text.Json;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Service for loading and managing application configuration
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;
    private readonly AppConfiguration _configuration;
    private readonly string _configDirectory;

    public ConfigurationService(Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _logger = logger;
        _configDirectory = GetConfigDirectory();
        _configuration = new AppConfiguration();

        LoadConfiguration();
    }

    /// <summary>
    /// Gets the current application configuration
    /// </summary>
    public AppConfiguration GetConfiguration() => _configuration;

    /// <summary>
    /// Gets Azure OpenAI settings
    /// </summary>
    public AzureOpenAISettings GetAzureOpenAISettings() => _configuration.AzureOpenAI;

    /// <summary>
    /// Gets extraction settings
    /// </summary>
    public ExtractionSettings GetExtractionSettings() => _configuration.Extraction;

    /// <summary>
    /// Gets meeting type settings
    /// </summary>
    public MeetingTypeSettings GetMeetingTypeSettings() => _configuration.MeetingTypes;

    /// <summary>
    /// Gets language settings
    /// </summary>
    public LanguageSettings GetLanguageSettings() => _configuration.Languages;

    /// <summary>
    /// Gets prompt settings
    /// </summary>
    public PromptSettings GetPromptSettings() => _configuration.Prompts;

    /// <summary>
    /// Reloads configuration from files and environment variables
    /// </summary>
    public void ReloadConfiguration()
    {
        LoadConfiguration();
        _logger?.LogInformation("Configuration reloaded");
    }

    /// <summary>
    /// Saves current configuration to files
    /// </summary>
    public async Task SaveConfigurationAsync()
    {
        try
        {
            Directory.CreateDirectory(_configDirectory);

            // Save each configuration section separately for better maintainability
            await SaveConfigurationSectionAsync("azure-openai.json", _configuration.AzureOpenAI);
            await SaveConfigurationSectionAsync("extraction.json", _configuration.Extraction);
            await SaveConfigurationSectionAsync("meeting-types.json", _configuration.MeetingTypes);
            await SaveConfigurationSectionAsync("languages.json", _configuration.Languages);
            await SaveConfigurationSectionAsync("prompts.json", _configuration.Prompts);

            _logger?.LogInformation($"Configuration saved to {_configDirectory}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save configuration");
            throw;
        }
    }

    public async Task SaveAzureOpenAISettingsAsync(AzureOpenAISettings settings)
    {
        try
        {
            Directory.CreateDirectory(_configDirectory);

            // Update in-memory configuration
            _configuration.AzureOpenAI = settings;

            // Save to disk
            await SaveConfigurationSectionAsync("azure-openai.json", settings);

            _logger?.LogInformation("Azure OpenAI settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save Azure OpenAI settings");
            throw;
        }
    }

    /// <summary>
    /// Gets a formatted prompt for action item extraction
    /// </summary>
    public string GetExtractionPrompt(MeetingTranscript transcript, string? meetingType = null, string? language = null)
    {
        var prompt = _configuration.Prompts.BaseExtractionPrompt;

        // Replace template variables
        prompt = prompt.Replace("{title}", transcript.Title ?? "Meeting");
        prompt = prompt.Replace("{date}", transcript.MeetingDate.ToString("yyyy-MM-dd"));
        prompt = prompt.Replace("{participants}", string.Join(", ", transcript.Participants));
        prompt = prompt.Replace("{content}", transcript.Content);
        prompt = prompt.Replace("{actionKeywords}", string.Join(", ", _configuration.Extraction.ActionKeywords));

        // Add meeting type specific guidance if provided
        if (!string.IsNullOrEmpty(meetingType) && _configuration.Prompts.MeetingTypeGuidance.ContainsKey(meetingType))
        {
            prompt += $"\n\nMeeting Type Guidance: {_configuration.Prompts.MeetingTypeGuidance[meetingType]}";
        }

        // Add language specific instructions if provided
        if (!string.IsNullOrEmpty(language) && _configuration.Prompts.ConsistencyRules.ContainsKey(language))
        {
            prompt += $"\n\nConsistency Rules: {_configuration.Prompts.ConsistencyRules[language]}";
        }

        return prompt;
    }

    /// <summary>
    /// Gets system prompt for AI, optionally for specific language
    /// </summary>
    public string GetSystemPrompt(string? language = null)
    {
        if (!string.IsNullOrEmpty(language) && _configuration.Prompts.LanguagePrompts.ContainsKey(language))
        {
            return _configuration.Prompts.LanguagePrompts[language];
        }

        return _configuration.AzureOpenAI.SystemPrompt;
    }

    /// <summary>
    /// Determines configuration directory path
    /// </summary>
    private string GetConfigDirectory()
    {
        // Try to find project root directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName ?? baseDir;

        // Check if .env file exists to confirm project root
        if (File.Exists(Path.Combine(projectRoot, ".env")))
        {
            return Path.Combine(projectRoot, "config");
        }

        // Fallback to base directory
        return Path.Combine(baseDir, "config");
    }

    /// <summary>
    /// Loads configuration from files and environment variables
    /// </summary>
    private void LoadConfiguration()
    {
        try
        {
            // Load from configuration files first (if they exist)
            LoadFromConfigurationFiles();

            // Override with environment variables
            LoadFromEnvironmentVariables();

            _logger?.LogInformation("Configuration loaded successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Failed to load some configuration, using defaults. Error: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Loads configuration from JSON files
    /// </summary>
    private void LoadFromConfigurationFiles()
    {
        LoadConfigurationSection("azure-openai.json", (AzureOpenAISettings settings) => _configuration.AzureOpenAI = settings);
        LoadConfigurationSection("extraction.json", (ExtractionSettings settings) => _configuration.Extraction = settings);
        LoadConfigurationSection("meeting-types.json", (MeetingTypeSettings settings) => _configuration.MeetingTypes = settings);
        LoadConfigurationSection("languages.json", (LanguageSettings settings) => _configuration.Languages = settings);
        LoadConfigurationSection("prompts.json", (PromptSettings settings) => _configuration.Prompts = settings);
    }

    /// <summary>
    /// Loads a specific configuration section from file
    /// </summary>
    private void LoadConfigurationSection<T>(string fileName, Action<T> setConfiguration) where T : new()
    {
        var filePath = Path.Combine(_configDirectory, fileName);
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                });

                if (settings != null)
                {
                    setConfiguration(settings);
                    _logger?.LogDebug("Loaded configuration from {FileName}", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Failed to load configuration from {FileName}. Error: {Error}", fileName, ex.Message);
            }
        }
    }

    /// <summary>
    /// Loads configuration from environment variables
    /// </summary>
    private void LoadFromEnvironmentVariables()
    {
        // Azure OpenAI settings
        SetIfNotNull(_configuration.AzureOpenAI, nameof(AzureOpenAISettings.Endpoint),
            Environment.GetEnvironmentVariable("AOAI_ENDPOINT") ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"));

        SetIfNotNull(_configuration.AzureOpenAI, nameof(AzureOpenAISettings.ApiKey),
            Environment.GetEnvironmentVariable("AOAI_APIKEY") ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"));

        SetIfNotNull(_configuration.AzureOpenAI, nameof(AzureOpenAISettings.DeploymentName),
            Environment.GetEnvironmentVariable("CHATCOMPLETION_DEPLOYMENTNAME") ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME"));

        // Azure OpenAI parameters
        SetIfNotNull(_configuration.AzureOpenAI, nameof(AzureOpenAISettings.MaxTokens),
            GetIntFromEnv("AI_MAX_TOKENS"));

        SetIfNotNull(_configuration.AzureOpenAI, nameof(AzureOpenAISettings.Temperature),
            GetDoubleFromEnv("AI_TEMPERATURE"));

        SetIfNotNull(_configuration.AzureOpenAI, nameof(AzureOpenAISettings.TopP),
            GetDoubleFromEnv("AI_TOP_P"));

        SetIfNotNull(_configuration.AzureOpenAI, nameof(AzureOpenAISettings.ApiVersion),
            Environment.GetEnvironmentVariable("AI_API_VERSION"));

        SetIfNotNull(_configuration.AzureOpenAI, nameof(AzureOpenAISettings.SystemPrompt),
            Environment.GetEnvironmentVariable("AI_SYSTEM_PROMPT"));

        // Extraction settings
        SetListFromEnv(_configuration.Extraction, nameof(ExtractionSettings.ActionKeywords), "EXTRACTION_ACTION_KEYWORDS");
        SetListFromEnv(_configuration.Extraction, nameof(ExtractionSettings.BugKeywords), "EXTRACTION_BUG_KEYWORDS");
        SetListFromEnv(_configuration.Extraction, nameof(ExtractionSettings.InvestigationKeywords), "EXTRACTION_INVESTIGATION_KEYWORDS");
        SetListFromEnv(_configuration.Extraction, nameof(ExtractionSettings.DocumentationKeywords), "EXTRACTION_DOCUMENTATION_KEYWORDS");
        SetListFromEnv(_configuration.Extraction, nameof(ExtractionSettings.ReviewKeywords), "EXTRACTION_REVIEW_KEYWORDS");
        SetListFromEnv(_configuration.Extraction, nameof(ExtractionSettings.StoryKeywords), "EXTRACTION_STORY_KEYWORDS");

        SetIfNotNull(_configuration.Extraction, nameof(ExtractionSettings.MaxTitleLength),
            GetIntFromEnv("EXTRACTION_MAX_TITLE_LENGTH"));

        // Meeting type settings
        SetIfNotNull(_configuration.MeetingTypes, nameof(MeetingTypeSettings.OneOnOneMaxParticipants),
            GetIntFromEnv("MEETING_ONEONONE_MAX_PARTICIPANTS"));

        SetIfNotNull(_configuration.MeetingTypes, nameof(MeetingTypeSettings.StandupMaxContentLength),
            GetIntFromEnv("MEETING_STANDUP_MAX_CONTENT_LENGTH"));

        _logger?.LogDebug("Environment variable overrides applied");
    }

    /// <summary>
    /// Saves a configuration section to file
    /// </summary>
    private async Task SaveConfigurationSectionAsync<T>(string fileName, T settings)
    {
        var filePath = Path.Combine(_configDirectory, fileName);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Sets property value if the new value is not null
    /// </summary>
    private void SetIfNotNull<T>(object obj, string propertyName, T? value)
    {
        if (value != null)
        {
            var property = obj.GetType().GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                // Handle type conversion for value types
                if (property.PropertyType != typeof(T) && value != null)
                {
                    var converter = TypeDescriptor.GetConverter(property.PropertyType);
                    if (converter.CanConvertFrom(typeof(T)))
                    {
                        value = (T?)converter.ConvertFrom(value);
                    }
                }

                property.SetValue(obj, value);
            }
        }
    }

    /// <summary>
    /// Sets a list property from comma-separated environment variable
    /// </summary>
    private void SetListFromEnv(object obj, string propertyName, string envVarName)
    {
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrEmpty(envValue))
        {
            var values = envValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                               .Select(v => v.Trim())
                               .ToList();

            var property = obj.GetType().GetProperty(propertyName);
            if (property != null && property.CanWrite && property.PropertyType == typeof(List<string>))
            {
                property.SetValue(obj, values);
            }
        }
    }

    /// <summary>
    /// Gets integer value from environment variable
    /// </summary>
    private int? GetIntFromEnv(string envVarName)
    {
        var value = Environment.GetEnvironmentVariable(envVarName);
        return int.TryParse(value, out var result) ? result : null;
    }

    /// <summary>
    /// Gets double value from environment variable
    /// </summary>
    private double? GetDoubleFromEnv(string envVarName)
    {
        var value = Environment.GetEnvironmentVariable(envVarName);
        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;
    }
}
