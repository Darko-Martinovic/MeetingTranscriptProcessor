using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using MeetingTranscriptProcessor.Services;

namespace MeetingTranscriptProcessor.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class TrainingController(
    ITranscriptProcessorService transcriptProcessor,
    IConfigurationService configurationService,
    ILogger<TrainingController> logger) : ControllerBase
{
    /// <summary>
    /// Extracts the action items of a transcript to show them, at the given temperature;
    /// nothing is archived and no Jira tickets are created
    /// </summary>
    [HttpPost("process")]
    public async Task<ActionResult<TrainingResult>> ProcessTranscript([FromForm] IFormFile file, [FromForm] double? temperature)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });
        if (temperature is < 0 or > 2)
            return BadRequest(new { error = "Temperature must be between 0 and 2" });

        var fileName = MeetingFileNames.FromUpload(file.FileName);
        if (fileName == null)
            return BadRequest(new { error = "Invalid file name" });

        // The file goes in a temporary folder of its own; its name is kept for the title
        var tempFolder = Directory.CreateTempSubdirectory("mtp-training-").FullName;
        try
        {
            var tempPath = Path.Combine(tempFolder, fileName);
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

            var aiSettings = configurationService.GetAzureOpenAISettings();
            var usedTemperature = temperature ?? aiSettings.Temperature;
            var transcript = await transcriptProcessor.ProcessTranscriptAsync(tempPath, usedTemperature);

            logger.LogInformation(
                "Training run on {FileName} at temperature {Temperature}: {Count} action items",
                fileName, usedTemperature, transcript.ActionItems.Count);

            return new TrainingResult
            {
                FileName = fileName,
                TokensUsed = transcript.TokensUsed ?? 0,
                EstimatedCost = transcript.EstimatedCost ?? 0,
                ModelName = aiSettings.DeploymentName,
                Temperature = usedTemperature,
                MaxTokens = aiSettings.MaxTokens,
                ActionItems = transcript.ActionItems.Select((item, index) => new TrainingActionItem
                {
                    Id = item.Id,
                    TicketNumber = $"TRAIN-{index + 1}",
                    Title = item.Title,
                    Description = item.Description,
                    Priority = item.Priority.ToString(),
                    Type = item.Type.ToString(),
                    AssignedTo = item.AssignedTo
                }).ToList()
            };
        }
        finally
        {
            Directory.Delete(tempFolder, recursive: true);
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
