using DotNetEnv;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using MeetingTranscriptProcessor.Services;

// Keys and settings come from .env in the working directory; the tests bring their own
if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Testing")
{
    Env.Load();
}

// --web and --api chose the web mode when there was also a console mode; they are ignored now
var builder = WebApplication.CreateBuilder(args.Where(arg => arg is not ("--web" or "--api")).ToArray());

builder.Services.AddSingleton<StoragePaths>();
builder.Services.AddSingleton<MeetingTranscriptProcessor.Services.ILogger, ConsoleLogger>();
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<IAzureOpenAIService, AzureOpenAIService>();
builder.Services.AddSingleton<ITranscriptProcessorService, TranscriptProcessorService>();
builder.Services.AddSingleton<IJiraTicketService, JiraTicketService>();
builder.Services.AddSingleton<IProcessingStatusService, ProcessingStatusService>();
builder.Services.AddSingleton<IActionItemValidator, ActionItemValidator>();
builder.Services.AddSingleton<IHallucinationDetector, HallucinationDetector>();
builder.Services.AddSingleton<IConsistencyManager, ConsistencyManager>();
builder.Services.AddSingleton<SmartTitleGeneratorService>();
builder.Services.AddSingleton<IFileWatcherService>(provider =>
{
    var paths = provider.GetRequiredService<StoragePaths>();
    return new FileWatcherService(
        paths.Incoming,
        paths.Processing,
        provider.GetService<MeetingTranscriptProcessor.Services.ILogger>()
    );
});
builder.Services.AddHostedService<TranscriptWorker>();

builder.Services.AddControllers();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddCors();

var app = builder.Build();

// The frontend's origins, from CORS_ORIGINS (comma-separated); by default the Vite dev server ports on localhost
var corsOrigins = (app.Configuration["CORS_ORIGINS"] is { Length: > 0 } origins
        ? origins
        : "http://localhost:5173,http://localhost:5175,http://localhost:3000,http://localhost:5273")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// UseCors must come between UseRouting and MapControllers
app.UseRouting();
app.UseCors(policy => policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod());
app.UseMiddleware<ApiKeyMiddleware>();
app.MapControllers();

// Without API_KEY, the API may only be reached from this machine
app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
    var exposed = addresses.Where(address => !NetworkExposure.IsLoopback(address)).ToList();
    if (exposed.Count > 0 && string.IsNullOrEmpty(app.Configuration["API_KEY"]))
    {
        app.Logger.LogCritical(
            "Listening on {Addresses} reaches other machines; set API_KEY in .env to allow it. Stopping",
            string.Join(", ", exposed));
        app.Lifetime.StopApplication();
    }
});

app.Run();

/// <summary>
/// The entry point, public so the tests can start the app
/// </summary>
public partial class Program;
