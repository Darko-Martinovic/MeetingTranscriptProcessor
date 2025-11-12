using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using MeetingTranscriptProcessor.Services;

namespace MeetingTranscriptProcessor.Controllers;

[ApiController]
[Route("api/processing")]
[EnableCors("AllowFrontend")]
public class ProcessingController : ControllerBase
{
    private readonly IProcessingStatusService _processingStatusService;

    public ProcessingController(IProcessingStatusService processingStatusService)
    {
        _processingStatusService = processingStatusService;
    }

    /// <summary>
    /// Get the current processing queue and status
    /// </summary>
    [HttpGet("queue")]
    public ActionResult GetProcessingQueue()
    {
        try
        {
            var queue = _processingStatusService.GetQueue();
            return Ok(queue);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get processing queue", details = ex.Message });
        }
    }

    /// <summary>
    /// Get status for a specific processing job
    /// </summary>
    [HttpGet("status/{id}")]
    public ActionResult GetProcessingStatus(string id)
    {
        try
        {
            var status = _processingStatusService.GetStatus(id);
            if (status == null)
            {
                return NotFound(new { error = $"Processing job {id} not found" });
            }
            return Ok(status);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get processing status", details = ex.Message });
        }
    }

    /// <summary>
    /// Clear completed processing history
    /// </summary>
    [HttpDelete("completed")]
    public ActionResult ClearCompleted()
    {
        try
        {
            _processingStatusService.ClearCompleted();
            return Ok(new { message = "Completed processing history cleared" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to clear completed history", details = ex.Message });
        }
    }
}
