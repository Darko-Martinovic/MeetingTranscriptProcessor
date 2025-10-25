using Microsoft.AspNetCore.Mvc;
using MeetingTranscriptProcessor.Models;
using MeetingTranscriptProcessor.Services;

namespace MeetingTranscriptProcessor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private readonly IConfigurationService _configurationService;

        public ConfigurationController(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        [HttpGet]
        public ActionResult<ConfigurationDto> GetConfiguration()
        {
            try
            {
                var config = _configurationService.GetConfiguration();
                var azureOpenAI = _configurationService.GetAzureOpenAISettings();
                var extraction = _configurationService.GetExtractionSettings();

                var dto = new ConfigurationDto
                {
                    AzureOpenAI = new AzureOpenAIDto
                    {
                        Endpoint = azureOpenAI.Endpoint ?? string.Empty,
                        DeploymentName = azureOpenAI.DeploymentName,
                        ApiVersion = azureOpenAI.ApiVersion,
                        IsConfigured =
                            !string.IsNullOrEmpty(azureOpenAI.Endpoint)
                            && !string.IsNullOrEmpty(azureOpenAI.ApiKey),
                        SystemPrompt = azureOpenAI.SystemPrompt,
                        CustomPrompt = azureOpenAI.CustomPrompt
                    },
                    Extraction = new ExtractionDto
                    {
                        MaxConcurrentFiles = int.Parse(
                            Environment.GetEnvironmentVariable("MAX_CONCURRENT_FILES") ?? "3"
                        ),
                        ValidationConfidenceThreshold = double.Parse(
                            Environment.GetEnvironmentVariable("VALIDATION_CONFIDENCE_THRESHOLD")
                                ?? "0.5"
                        ),
                        EnableValidation = bool.Parse(
                            Environment.GetEnvironmentVariable("ENABLE_VALIDATION") ?? "true"
                        ),
                        EnableHallucinationDetection = bool.Parse(
                            Environment.GetEnvironmentVariable("ENABLE_HALLUCINATION_DETECTION")
                                ?? "true"
                        ),
                        EnableConsistencyManagement = bool.Parse(
                            Environment.GetEnvironmentVariable("ENABLE_CONSISTENCY_MANAGEMENT")
                                ?? "true"
                        )
                    },
                    Environment = new EnvironmentDto
                    {
                        IncomingDirectory = Environment.GetEnvironmentVariable(
                            "INCOMING_DIRECTORY"
                        ),
                        ProcessingDirectory = Environment.GetEnvironmentVariable(
                            "PROCESSING_DIRECTORY"
                        ),
                        ArchiveDirectory = Environment.GetEnvironmentVariable("ARCHIVE_DIRECTORY"),
                        JiraUrl = Environment.GetEnvironmentVariable("JIRA_URL"),
                        JiraEmail = Environment.GetEnvironmentVariable("JIRA_EMAIL"),
                        JiraDefaultProject = Environment.GetEnvironmentVariable(
                            "JIRA_DEFAULT_PROJECT"
                        ),
                        IsJiraConfigured = IsJiraConfigured()
                    }
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("azure-openai")]
        public ActionResult UpdateAzureOpenAIConfiguration([FromBody] AzureOpenAIUpdateDto dto)
        {
            try
            {
                // Update environment variables
                if (!string.IsNullOrEmpty(dto.Endpoint))
                    Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", dto.Endpoint);

                if (!string.IsNullOrEmpty(dto.ApiKey))
                    Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", dto.ApiKey);

                if (!string.IsNullOrEmpty(dto.DeploymentName))
                    Environment.SetEnvironmentVariable(
                        "AZURE_OPENAI_DEPLOYMENT_NAME",
                        dto.DeploymentName
                    );

                // Reload configuration
                _configurationService.ReloadConfiguration();

                return Ok(new { message = "Azure OpenAI configuration updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("prompts")]
        public async Task<ActionResult> UpdatePromptsConfiguration([FromBody] PromptsUpdateDto dto)
        {
            try
            {
                var settings = _configurationService.GetAzureOpenAISettings();

                // Update custom prompt
                settings.CustomPrompt = dto.CustomPrompt;

                // Save to configuration file
                await _configurationService.SaveAzureOpenAISettingsAsync(settings);

                return Ok(new { message = "Prompts configuration updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("extraction")]
        public ActionResult UpdateExtractionConfiguration([FromBody] ExtractionUpdateDto dto)
        {
            try
            {
                // Update environment variables
                if (dto.MaxConcurrentFiles.HasValue)
                    Environment.SetEnvironmentVariable(
                        "MAX_CONCURRENT_FILES",
                        dto.MaxConcurrentFiles.ToString()
                    );

                if (dto.ValidationConfidenceThreshold.HasValue)
                    Environment.SetEnvironmentVariable(
                        "VALIDATION_CONFIDENCE_THRESHOLD",
                        dto.ValidationConfidenceThreshold.ToString()
                    );

                if (dto.EnableValidation.HasValue)
                    Environment.SetEnvironmentVariable(
                        "ENABLE_VALIDATION",
                        dto.EnableValidation.ToString()
                    );

                if (dto.EnableHallucinationDetection.HasValue)
                    Environment.SetEnvironmentVariable(
                        "ENABLE_HALLUCINATION_DETECTION",
                        dto.EnableHallucinationDetection.ToString()
                    );

                if (dto.EnableConsistencyManagement.HasValue)
                    Environment.SetEnvironmentVariable(
                        "ENABLE_CONSISTENCY_MANAGEMENT",
                        dto.EnableConsistencyManagement.ToString()
                    );

                // Reload configuration
                _configurationService.ReloadConfiguration();

                return Ok(new { message = "Extraction configuration updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("jira")]
        public ActionResult UpdateJiraConfiguration([FromBody] JiraUpdateDto dto)
        {
            try
            {
                // Update environment variables
                if (!string.IsNullOrEmpty(dto.Url))
                    Environment.SetEnvironmentVariable("JIRA_URL", dto.Url);

                if (!string.IsNullOrEmpty(dto.Email))
                    Environment.SetEnvironmentVariable("JIRA_EMAIL", dto.Email);

                if (!string.IsNullOrEmpty(dto.ApiToken))
                    Environment.SetEnvironmentVariable("JIRA_API_TOKEN", dto.ApiToken);

                if (!string.IsNullOrEmpty(dto.DefaultProject))
                    Environment.SetEnvironmentVariable("JIRA_DEFAULT_PROJECT", dto.DefaultProject);

                return Ok(new { message = "Jira configuration updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("system-status")]
        public ActionResult<SystemStatusDto> GetSystemStatus()
        {
            try
            {
                var status = new SystemStatusDto
                {
                    IsRunning = true, // API is running if we can respond
                    AzureOpenAIConfigured = !string.IsNullOrEmpty(
                        Environment.GetEnvironmentVariable("AOAI_ENDPOINT") ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                    ) && !string.IsNullOrEmpty(
                        Environment.GetEnvironmentVariable("AOAI_APIKEY") ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
                    ) && !string.IsNullOrEmpty(
                        Environment.GetEnvironmentVariable("CHATCOMPLETION_DEPLOYMENTNAME") ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
                    ),
                    JiraConfigured = IsJiraConfigured(),
                    ValidationEnabled = bool.Parse(
                        Environment.GetEnvironmentVariable("ENABLE_VALIDATION") ?? "true"
                    ),
                    HallucinationDetectionEnabled = bool.Parse(
                        Environment.GetEnvironmentVariable("ENABLE_HALLUCINATION_DETECTION")
                            ?? "true"
                    ),
                    ConsistencyManagementEnabled = bool.Parse(
                        Environment.GetEnvironmentVariable("ENABLE_CONSISTENCY_MANAGEMENT")
                            ?? "true"
                    ),
                    CurrentTime = DateTime.UtcNow
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private bool IsJiraConfigured()
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JIRA_URL"))
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JIRA_API_TOKEN"))
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JIRA_EMAIL"));
        }
    }
}
