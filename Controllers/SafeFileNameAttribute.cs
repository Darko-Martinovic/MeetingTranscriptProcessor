using MeetingTranscriptProcessor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MeetingTranscriptProcessor.Controllers;

/// <summary>
/// Refuses a request whose fileName isn't a plain file name (see <see cref="MeetingFileNames"/>)
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SafeFileNameAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("fileName", out var value)
            && !MeetingFileNames.IsSafe(value as string))
        {
            context.Result = new BadRequestObjectResult(new { error = "Invalid file name" });
        }
    }
}
