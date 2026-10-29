using Asp.Versioning;
using MeetingTranscriptProcessor.Models;
using MeetingTranscriptProcessor.Services;
using Microsoft.AspNetCore.Mvc;

namespace MeetingTranscriptProcessor.Controllers;

/// <summary>
/// The meeting folders and files; the work is done by <see cref="MeetingStore"/>
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[SafeFileName]
public class MeetingsController(MeetingStore store) : ControllerBase
{
    private static readonly object MeetingNotFound = new { error = "Meeting file not found" };

    [HttpGet("folders")]
    public async Task<ActionResult<List<FolderInfo>>> GetFolders() => await store.GetFoldersAsync();

    [HttpGet("folders/{folderType}/meetings")]
    public async Task<ActionResult<List<MeetingInfo>>> GetMeetingsInFolder(
        FolderType folderType,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? language = null,
        [FromQuery] string? participants = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] bool? hasJiraTickets = null,
        [FromQuery] string sortBy = "date",
        [FromQuery] string sortOrder = "desc",
        [FromQuery] string? favoriteFileNames = null)
    {
        if (!Enum.IsDefined(folderType))
            return BadRequest(new { error = "Invalid folder type" });

        return await store.GetMeetingsAsync(folderType, new MeetingQuery(
            search, status, language, participants, dateFrom, dateTo, hasJiraTickets, sortBy, sortOrder, favoriteFileNames));
    }

    [HttpGet("meeting/{fileName}")]
    public async Task<ActionResult<MeetingTranscript>> GetMeeting(string fileName) =>
        await store.GetMeetingAsync(fileName) is { } transcript ? transcript : NotFound(MeetingNotFound);

    [HttpGet("meeting/{fileName}/tickets")]
    public async Task<ActionResult<List<JiraTicketReference>>> GetMeetingJiraTickets(string fileName) =>
        await store.GetTicketsAsync(fileName);

    [HttpPost("upload")]
    public async Task<ActionResult> UploadMeeting(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        // Only the last part of the name the browser sent is kept
        var originalName = MeetingFileNames.FromUpload(file.FileName);
        if (originalName == null)
            return BadRequest(new { error = "Invalid file name" });
        if (!TranscriptReader.IsSupported(originalName))
            return BadRequest(new { error = "File type not supported" });

        await using var content = file.OpenReadStream();
        var fileName = await store.AddToIncomingAsync(originalName, content);

        // The background worker picks the file up from the incoming folder
        return Ok(new
        {
            message = "File uploaded successfully and queued for processing",
            fileName,
            status = "uploaded"
        });
    }

    [HttpDelete("meeting/{fileName}")]
    public ActionResult DeleteMeeting(string fileName) =>
        store.Delete(fileName) ? Ok(new { message = "Meeting deleted successfully" }) : NotFound(MeetingNotFound);

    [HttpPut("meeting/{fileName}/title")]
    public async Task<ActionResult> UpdateMeetingTitle(string fileName, [FromBody] UpdateMeetingTitleDto request)
    {
        var title = request.Title.Trim();
        if (title.Length == 0)
            return BadRequest(new { error = "Title cannot be empty" });

        var change = await store.UpdateTitleAsync(fileName, title);
        return Result(change, () => Ok(new { message = change.Message, title }));
    }

    [HttpPut("meeting/{fileName}/move")]
    public ActionResult MoveMeeting(string fileName, [FromBody] MoveMeetingDto request)
    {
        var change = store.Move(fileName, request.TargetFolderType);
        return Result(change, () => Ok(new
        {
            message = change.Message,
            fileName = change.FileName,
            targetFolder = request.TargetFolderType
        }));
    }

    private ActionResult Result(MeetingChange change, Func<ActionResult> done) =>
        !change.Found ? NotFound(MeetingNotFound)
        : change.Error != null ? BadRequest(new { error = change.Error })
        : done();
}
