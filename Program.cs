using DotNetEnv;
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:3000",      // Vite dev server
                  "http://localhost:5173",      // Vite dev server (alternate)
                  "http://localhost:5175",      // Vite dev server (alternate)
                  "http://localhost:5273",      // Python HTTP server (production)
                  "http://10.74.42.54:5273",    // Remote machine frontend
                  "http://10.74.42.54:5100"     // Remote machine backend (for API calls)
              )
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// UseCors must come between UseRouting and MapControllers
app.UseRouting();
app.UseCors("AllowFrontend");
app.MapControllers();

app.Run();

/// <summary>
/// The entry point, public so the tests can start the app
/// </summary>
public partial class Program;
