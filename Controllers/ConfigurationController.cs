using Asp.Versioning;
using MeetingTranscriptProcessor.Models;
using MeetingTranscriptProcessor.Services;
using Microsoft.AspNetCore.Mvc;

namespace MeetingTranscriptProcessor.Controllers;

/// <summary>
/// The settings the Settings page shows and changes. Credentials come from .env and are only
/// reported as configured or not
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class ConfigurationController(
    IConfigurationService configurationService,
    IConfiguration configuration,
    StoragePaths paths) : ControllerBase
{
    [HttpGet]
    public ActionResult<ConfigurationDto> GetConfiguration()
    {
        var azureOpenAI = configurationService.GetAzureOpenAISettings();
        var processing = configurationService.GetProcessingSettings();

        return new ConfigurationDto
        {
            AzureOpenAI = new AzureOpenAIDto
            {
                Endpoint = azureOpenAI.Endpoint ?? string.Empty,
                DeploymentName = azureOpenAI.DeploymentName,
                Temperature = azureOpenAI.Temperature,
                MaxTokens = azureOpenAI.MaxTokens,
                TopP = azureOpenAI.TopP,
                IsConfigured = IsAzureOpenAIConfigured(azureOpenAI),
                SystemPrompt = azureOpenAI.SystemPrompt,
                CustomPrompt = azureOpenAI.CustomPrompt
            },
            Extraction = new ExtractionDto
            {
                MaxConcurrentFiles = processing.MaxConcurrentFiles,
                ValidationConfidenceThreshold = processing.ValidationConfidenceThreshold,
                EnableValidation = processing.EnableValidation,
                EnableHallucinationDetection = processing.EnableHallucinationDetection,
                EnableConsistencyManagement = processing.EnableConsistencyManagement
            },
            Environment = new EnvironmentDto
            {
                IncomingDirectory = paths.Incoming,
                ProcessingDirectory = paths.Processing,
                ArchiveDirectory = paths.Archive,
                JiraUrl = configuration["JIRA_URL"],
                JiraEmail = configuration["JIRA_EMAIL"],
                JiraDefaultProject = configuration["JIRA_PROJECT_KEY"] is { Length: > 0 } key ? key : configuration["JIRA_DEFAULT_PROJECT"],
                IsJiraConfigured = IsJiraConfigured()
            }
        };
    }

    /// <summary>
    /// Updates the model settings and saves them in config/azure-openai.json.
    /// The endpoint and the key are set in .env only
    /// </summary>
    [HttpPut("azure-openai")]
    public async Task<ActionResult> UpdateAzureOpenAIConfiguration([FromBody] AzureOpenAIUpdateDto dto)
    {
        var settings = configurationService.GetAzureOpenAISettings();

        if (!string.IsNullOrWhiteSpace(dto.DeploymentName))
            settings.DeploymentName = dto.DeploymentName.Trim();
        if (dto.Temperature.HasValue)
            settings.Temperature = dto.Temperature.Value;
        if (dto.MaxTokens.HasValue)
            settings.MaxTokens = dto.MaxTokens.Value;
        if (dto.TopP.HasValue)
            settings.TopP = dto.TopP.Value;

        await configurationService.SaveAzureOpenAISettingsAsync(settings);
        return Ok(new { message = "Azure OpenAI configuration updated successfully" });
    }

    /// <summary>
    /// Saves the guidance added to the system prompt (the training page's custom prompt)
    /// </summary>
    [HttpPut("prompts")]
    public async Task<ActionResult> UpdatePromptsConfiguration([FromBody] PromptsUpdateDto dto)
    {
        var settings = configurationService.GetAzureOpenAISettings();
        settings.CustomPrompt = dto.CustomPrompt;
        await configurationService.SaveAzureOpenAISettingsAsync(settings);
        return Ok(new { message = "Prompts configuration updated successfully" });
    }

    /// <summary>
    /// Updates the processing settings and saves them in config/processing.json; the next file uses them
    /// </summary>
    [HttpPut("extraction")]
    public async Task<ActionResult> UpdateExtractionConfiguration([FromBody] ExtractionUpdateDto dto)
    {
        var settings = configurationService.GetProcessingSettings();

        if (dto.MaxConcurrentFiles.HasValue)
            settings.MaxConcurrentFiles = dto.MaxConcurrentFiles.Value;
        if (dto.ValidationConfidenceThreshold.HasValue)
            settings.ValidationConfidenceThreshold = dto.ValidationConfidenceThreshold.Value;
        if (dto.EnableValidation.HasValue)
            settings.EnableValidation = dto.EnableValidation.Value;
        if (dto.EnableHallucinationDetection.HasValue)
            settings.EnableHallucinationDetection = dto.EnableHallucinationDetection.Value;
        if (dto.EnableConsistencyManagement.HasValue)
            settings.EnableConsistencyManagement = dto.EnableConsistencyManagement.Value;

        await configurationService.SaveProcessingSettingsAsync(settings);
        return Ok(new { message = "Extraction configuration updated successfully" });
    }

    [HttpGet("system-status")]
    public ActionResult<SystemStatusDto> GetSystemStatus()
    {
        var processing = configurationService.GetProcessingSettings();
        return new SystemStatusDto
        {
            IsRunning = true,
            AzureOpenAIConfigured = IsAzureOpenAIConfigured(configurationService.GetAzureOpenAISettings()),
            JiraConfigured = IsJiraConfigured(),
            ValidationEnabled = processing.EnableValidation,
            HallucinationDetectionEnabled = processing.EnableHallucinationDetection,
            ConsistencyManagementEnabled = processing.EnableConsistencyManagement,
            CurrentTime = DateTime.UtcNow
        };
    }

    private static bool IsAzureOpenAIConfigured(AzureOpenAISettings settings) =>
        !string.IsNullOrEmpty(settings.Endpoint)
        && !string.IsNullOrEmpty(settings.ApiKey)
        && !string.IsNullOrEmpty(settings.DeploymentName);

    private bool IsJiraConfigured() =>
        !string.IsNullOrWhiteSpace(configuration["JIRA_URL"])
        && !string.IsNullOrWhiteSpace(configuration["JIRA_API_TOKEN"])
        && !string.IsNullOrWhiteSpace(configuration["JIRA_EMAIL"]);
}
