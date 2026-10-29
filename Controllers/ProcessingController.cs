using Asp.Versioning;
using MeetingTranscriptProcessor.Services;
using Microsoft.AspNetCore.Mvc;

namespace MeetingTranscriptProcessor.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/processing")]
[ApiVersion("1.0")]
public class ProcessingController(IProcessingStatusService processingStatusService) : ControllerBase
{
    /// <summary>
    /// Get the current processing queue and status
    /// </summary>
    [HttpGet("queue")]
    public ActionResult GetProcessingQueue() => Ok(processingStatusService.GetQueue());

    /// <summary>
    /// Get status for a specific processing job
    /// </summary>
    [HttpGet("status/{id}")]
    public ActionResult GetProcessingStatus(string id) =>
        processingStatusService.GetStatus(id) is { } status
            ? Ok(status)
            : NotFound(new { error = $"Processing job {id} not found" });

    /// <summary>
    /// Clear completed processing history
    /// </summary>
    [HttpDelete("completed")]
    public ActionResult ClearCompleted()
    {
        processingStatusService.ClearCompleted();
        return Ok(new { message = "Completed processing history cleared" });
    }
}
