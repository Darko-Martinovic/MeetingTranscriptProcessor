using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using MeetingTranscriptProcessor.Models;
using MeetingTranscriptProcessor.Services;

namespace MeetingTranscriptProcessor.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class TrainingController : ControllerBase
{
    private readonly ITranscriptProcessorService _transcriptProcessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TrainingController> _logger;

    public TrainingController(
        ITranscriptProcessorService transcriptProcessor,
        IConfiguration configuration,
        ILogger<TrainingController> logger)
    {
        _transcriptProcessor = transcriptProcessor;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("process")]
    public async Task<ActionResult<TrainingResult>> ProcessTranscript([FromForm] IFormFile file, [FromForm] double? temperature)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file provided" });
            }

            _logger.LogInformation($"Processing training transcript: {file.FileName} with temperature: {temperature ?? 0.1}");

            // Save file temporarily
            var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                // Get AI configuration for model information
                var configService = HttpContext.RequestServices.GetRequiredService<IConfigurationService>();
                var aiSettings = configService.GetAzureOpenAISettings();
                
                // Override temperature if provided
                if (temperature.HasValue)
                {
                    aiSettings.Temperature = temperature.Value;
                }
                
                // Process transcript (this will extract action items but we won't create JIRA tickets)
                var transcript = await _transcriptProcessor.ProcessTranscriptAsync(tempPath);

                // Convert to training result format
                var result = new TrainingResult
                {
                    FileName = file.FileName,
                    TokensUsed = transcript.TokensUsed ?? 0,
                    EstimatedCost = transcript.EstimatedCost ?? 0,
                    ModelName = aiSettings.DeploymentName,
                    Temperature = aiSettings.Temperature,
                    MaxTokens = aiSettings.MaxTokens,
                    ActionItems = transcript.ActionItems.Select((item, index) => new TrainingActionItem
                    {
                        Id = item.Id.ToString(),
                        TicketNumber = $"TRAIN-{index + 1}",
                        Title = item.Title,
                        Description = item.Description,
                        Priority = item.Priority.ToString(),
                        Type = item.Type.ToString(),
                        AssignedTo = item.AssignedTo
                    }).ToList()
                };

                _logger.LogInformation($"Successfully processed training transcript with {result.ModelName}. Found {result.ActionItems.Count} action items");

                return Ok(result);
            }
            finally
            {
                // Clean up temp file
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing training transcript");
            return StatusCode(500, new { error = "Failed to process transcript", details = ex.Message });
        }
    }

    [HttpPut("/api/configuration/custom-prompt")]
    public async Task<ActionResult> UpdateCustomPrompt([FromBody] CustomPromptUpdateDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.CustomPrompt))
            {
                return BadRequest(new { error = "Custom prompt cannot be empty" });
            }

            var configService = HttpContext.RequestServices.GetRequiredService<IConfigurationService>();
            var currentConfig = configService.GetAzureOpenAISettings();

            // Update custom prompt
            currentConfig.CustomPrompt = dto.CustomPrompt;

            // Save configuration
            await configService.SaveAzureOpenAISettingsAsync(currentConfig);

            _logger.LogInformation("Custom prompt updated successfully");

            return Ok(new { message = "Custom prompt updated successfully", customPrompt = dto.CustomPrompt });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating custom prompt");
            return StatusCode(500, new { error = "Failed to update custom prompt", details = ex.Message });
        }
    }
}

public class TrainingResult
{
    public string FileName { get; set; } = string.Empty;
    public List<TrainingActionItem> ActionItems { get; set; } = new();
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
}

public class TrainingActionItem
{
    public string Id { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
}

public class CustomPromptUpdateDto
{
    public string CustomPrompt { get; set; } = string.Empty;
}
