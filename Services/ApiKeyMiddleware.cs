using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// When API_KEY is set, every /api request must carry it in the X-Api-Key header
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public const string HeaderName = "X-Api-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        var expected = configuration["API_KEY"];
        if (!string.IsNullOrEmpty(expected)
            && context.Request.Path.StartsWithSegments("/api")
            && !HttpMethods.IsOptions(context.Request.Method)
            && !Matches(context.Request.Headers[HeaderName], expected))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = $"Missing or wrong {HeaderName} header" });
            return;
        }

        await next(context);
    }

    // Hashing first gives equal lengths, so the comparison takes the same time for any key
    private static bool Matches(string? given, string expected) =>
        given != null
        && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(given)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}

/// <summary>
/// Which addresses the app listens on reach beyond this machine
/// </summary>
public static class NetworkExposure
{
    /// <summary>
    /// True for localhost and loopback addresses; false for anything else, including
    /// the wildcards (*, +, 0.0.0.0 and [::]) that listen on every network
    /// </summary>
    public static bool IsLoopback(string address)
    {
        var withHost = address.Replace("://*", "://0.0.0.0").Replace("://+", "://0.0.0.0");
        if (!Uri.TryCreate(withHost, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.IsLoopback
            || (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip));
    }
}
