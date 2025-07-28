using MeetingTranscriptProcessor.Services;
using MeetingTranscriptProcessor.Models;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Concurrent;

namespace MeetingTranscriptProcessor
{
    /// <summary>
    /// Hybrid application that can run as both console app and web API
    /// </summary>
    internal class HybridProgram
    {
        private static IServiceProvider? _serviceProvider;
        private static IFileWatcherService? _fileWatcher;
        private static ITranscriptProcessorService? _transcriptProcessor;
        private static IJiraTicketService? _jiraTicketService;
        private static bool _isShuttingDown = false;
        private static bool _runAsWebApi = false;

        // Concurrency control
        private static SemaphoreSlim? _processingSemaphore;
        private static readonly ConcurrentDictionary<string, bool> _processingFiles = new();
        private static readonly CancellationTokenSource _cancellationTokenSource = new();
        private static int _maxConcurrentFiles = 3;

        // Validation service control
        private static bool _enableValidation = true;
        private static bool _enableHallucinationDetection = true;
        private static bool _enableConsistencyManagement = true;
        private static double _validationConfidenceThreshold = 0.5;

        // Directory paths
        private static string DataPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Data"
        );
        private static string IncomingPath = "";
        private static string ProcessingPath = "";
        private static string ArchivePath = "";

        public static async Task MainAsync(string[] args)
        {
            try
            {
                // Check if should run as web API
                _runAsWebApi = args.Contains("--web") || args.Contains("--api") || 
                              Environment.GetEnvironmentVariable("RUN_AS_WEB_API")?.ToLower() == "true";

                // Display application header
                DisplayHeader();

                // Load environment variables
                LoadEnvironment();

                if (_runAsWebApi)
                {
                    await RunAsWebApiAsync(args);
                }
                else
                {
                    await RunAsConsoleAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fatal error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            finally
            {
                await CleanupAsync();
            }
        }

        private static async Task RunAsWebApiAsync(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure services
            ConfigureWebApiServices(builder.Services);

            // Add controllers
            builder.Services.AddControllers();
            
            // Add CORS for frontend
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("AllowFrontend");
            app.UseRouting();
            app.MapControllers();

            // Serve static files for the React app
            app.UseStaticFiles();
            app.UseDefaultFiles();

            // Store service provider globally
            _serviceProvider = app.Services;

            // Initialize background services
            await InitializeBackgroundServicesAsync();

            Console.WriteLine("🌐 Web API Mode");
            Console.WriteLine($"🚀 Server starting on http://localhost:5000");
            Console.WriteLine("📁 Background file processing enabled");
            Console.WriteLine("⌨️  Press Ctrl+C to stop");

            await app.RunAsync();
        }

        private static async Task RunAsConsoleAsync()
        {
            // Initialize services for console mode
            InitializeServices();

            // Set up graceful shutdown
            Console.CancelKeyPress += OnCancelKeyPress;

            // Start file monitoring
            StartFileWatcher();

            // Display status and instructions
            DisplayStatus();

            // Keep the application running
            await RunMainLoopAsync();
        }

        private static void ConfigureWebApiServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging(builder => builder.AddConsole());

            // Register custom logger wrapper
            services.AddSingleton<MeetingTranscriptProcessor.Services.ILogger, ConsoleLogger>();

            // Register configuration service first
            services.AddSingleton<IConfigurationService, ConfigurationService>();

            // Register services
            services.AddSingleton<IAzureOpenAIService, AzureOpenAIService>();
            services.AddSingleton<ITranscriptProcessorService, TranscriptProcessorService>();
            services.AddSingleton<IJiraTicketService, JiraTicketService>();

            // Register validation and consistency services
            services.AddSingleton<IActionItemValidator, ActionItemValidator>();
            services.AddSingleton<IHallucinationDetector, HallucinationDetector>();
            services.AddSingleton<IConsistencyManager, ConsistencyManager>();

            services.AddSingleton<IFileWatcherService>(
                provider =>
                    new FileWatcherService(
                        IncomingPath,
                        ProcessingPath,
                        provider.GetService<MeetingTranscriptProcessor.Services.ILogger>()
                    )
            );
        }

        private static async Task InitializeBackgroundServicesAsync()
        {
            // Initialize concurrency semaphore
            _processingSemaphore = new SemaphoreSlim(_maxConcurrentFiles, _maxConcurrentFiles);

            // Ensure directories exist
            Directory.CreateDirectory(IncomingPath);
            Directory.CreateDirectory(ProcessingPath);
            Directory.CreateDirectory(ArchivePath);

            // Get services from DI container
            _fileWatcher = _serviceProvider?.GetRequiredService<IFileWatcherService>();
            _transcriptProcessor = _serviceProvider?.GetRequiredService<ITranscriptProcessorService>();
            _jiraTicketService = _serviceProvider?.GetRequiredService<IJiraTicketService>();

            // Setup event handlers
            if (_fileWatcher != null)
            {
                _fileWatcher.FileDetected += OnFileDetected;
                _fileWatcher.Start();
            }

            Console.WriteLine("✅ Background services initialized");
            await Task.CompletedTask; // Make this truly async
        }

        // Rest of the methods remain the same as in the original Program.cs
        // (DisplayHeader, LoadEnvironment, InitializeServices, etc.)

        private static void DisplayHeader()
        {
            Console.Clear();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║               Meeting Transcript Processor                  ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  Automatically processes meeting transcripts and creates    ║");
            Console.WriteLine("║  Jira tickets from extracted action items.                  ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine($"║  Mode: {(_runAsWebApi ? "Web API + Background Processing" : "Console Application").PadRight(42)} ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }

        private static void LoadEnvironment()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var projectRoot = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName ?? baseDir;
                var envPath = Path.Combine(projectRoot, ".env");

                if (File.Exists(envPath))
                {
                    Env.Load(envPath);
                    Console.WriteLine("✅ Environment configuration loaded from project root");
                }
                else
                {
                    envPath = Path.Combine(baseDir, ".env");
                    if (File.Exists(envPath))
                    {
                        Env.Load(envPath);
                        Console.WriteLine("✅ Environment configuration loaded");
                    }
                    else
                    {
                        Console.WriteLine("ℹ️  No .env file found - using system environment variables");
                    }
                }

                // Set directory paths
                var incomingEnv = Environment.GetEnvironmentVariable("INCOMING_DIRECTORY");
                var processingEnv = Environment.GetEnvironmentVariable("PROCESSING_DIRECTORY");
                var archiveEnv = Environment.GetEnvironmentVariable("ARCHIVE_DIRECTORY");

                IncomingPath = !string.IsNullOrEmpty(incomingEnv) && Path.IsPathRooted(incomingEnv)
                    ? incomingEnv
                    : Path.Combine(projectRoot, incomingEnv ?? "Data\\Incoming");

                ProcessingPath = !string.IsNullOrEmpty(processingEnv) && Path.IsPathRooted(processingEnv)
                    ? processingEnv
                    : Path.Combine(projectRoot, processingEnv ?? "Data\\Processing");

                ArchivePath = !string.IsNullOrEmpty(archiveEnv) && Path.IsPathRooted(archiveEnv)
                    ? archiveEnv
                    : Path.Combine(projectRoot, archiveEnv ?? "Data\\Archive");

                // Configure other settings (same as original)
                var maxConcurrentEnv = Environment.GetEnvironmentVariable("MAX_CONCURRENT_FILES");
                if (int.TryParse(maxConcurrentEnv, out var maxConcurrent) && maxConcurrent > 0 && maxConcurrent <= 10)
                {
                    _maxConcurrentFiles = maxConcurrent;
                }

                // ... (rest of the environment loading logic)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Warning: Could not load environment: {ex.Message}");
            }
        }

        // Include all other methods from the original Program.cs
        // (InitializeServices, StartFileWatcher, DisplayStatus, RunMainLoopAsync, OnFileDetected, etc.)
        // For brevity, I'll include the key ones:

        private static void InitializeServices()
        {
            Console.WriteLine("🔧 Initializing services...");

            _processingSemaphore = new SemaphoreSlim(_maxConcurrentFiles, _maxConcurrentFiles);

            Directory.CreateDirectory(IncomingPath);
            Directory.CreateDirectory(ProcessingPath);
            Directory.CreateDirectory(ArchivePath);

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<MeetingTranscriptProcessor.Services.ILogger, ConsoleLogger>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IAzureOpenAIService, AzureOpenAIService>();
            services.AddSingleton<ITranscriptProcessorService, TranscriptProcessorService>();
            services.AddSingleton<IJiraTicketService, JiraTicketService>();
            services.AddSingleton<IActionItemValidator, ActionItemValidator>();
            services.AddSingleton<IHallucinationDetector, HallucinationDetector>();
            services.AddSingleton<IConsistencyManager, ConsistencyManager>();
            services.AddSingleton<IFileWatcherService>(provider =>
                new FileWatcherService(IncomingPath, ProcessingPath, 
                    provider.GetService<MeetingTranscriptProcessor.Services.ILogger>()));

            _serviceProvider = services.BuildServiceProvider();
            _fileWatcher = _serviceProvider.GetRequiredService<IFileWatcherService>();
            _transcriptProcessor = _serviceProvider.GetRequiredService<ITranscriptProcessorService>();
            _jiraTicketService = _serviceProvider.GetRequiredService<IJiraTicketService>();
            _fileWatcher.FileDetected += OnFileDetected;

            Console.WriteLine("✅ Services initialized successfully");
        }

        private static void StartFileWatcher()
        {
            try
            {
                _fileWatcher?.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to start file watcher: {ex.Message}");
                throw;
            }
        }

        private static void DisplayStatus()
        {
            Console.WriteLine();
            Console.WriteLine("📊 System Status:");
            Console.WriteLine("──────────────────────────────────────────────────────────────");
            Console.WriteLine($"📁 Monitoring: {IncomingPath}");
            Console.WriteLine($"📁 Processing: {ProcessingPath}");
            Console.WriteLine($"📁 Archive: {ArchivePath}");
            Console.WriteLine("──────────────────────────────────────────────────────────────");
            Console.WriteLine();
            Console.WriteLine("🟢 System is running and monitoring for files...");
            Console.WriteLine();
        }

        private static async Task RunMainLoopAsync()
        {
            while (!_isShuttingDown)
            {
                try
                {
                    Console.Write("> ");
                    var input = Console.ReadLine()?.Trim().ToLowerInvariant();

                    switch (input)
                    {
                        case "quit":
                        case "exit":
                        case "q":
                            _isShuttingDown = true;
                            break;
                        case "status":
                        case "s":
                            DisplayStatus();
                            break;
                        case "help":
                        case "h":
                        case "?":
                            DisplayInstructions();
                            break;
                        case "":
                            break;
                        default:
                            Console.WriteLine($"Unknown command: '{input}'. Type 'help' for available commands.");
                            break;
                    }

                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error in main loop: {ex.Message}");
                }
            }
        }

        private static async void OnFileDetected(object? sender, FileDetectedEventArgs e)
        {
            await Task.Run(async () => await ProcessFileAsync(e.FilePath, e.FileName));
        }

        private static async Task ProcessFileAsync(string filePath, string fileName)
        {
            if (_isShuttingDown || _cancellationTokenSource.Token.IsCancellationRequested)
                return;

            if (!_processingFiles.TryAdd(filePath, true))
            {
                Console.WriteLine($"⚠️  File already being processed: {fileName}");
                return;
            }

            try
            {
                if (_processingSemaphore != null)
                {
                    await _processingSemaphore.WaitAsync(_cancellationTokenSource.Token);
                }

                try
                {
                    if (_isShuttingDown || _cancellationTokenSource.Token.IsCancellationRequested)
                        return;

                    Console.WriteLine($"\n📄 Processing file: {fileName}");
                    Console.WriteLine("──────────────────────────────────────────────────────────────");

                    var transcript = await _transcriptProcessor!.ProcessTranscriptAsync(filePath);

                    if (transcript.Status == TranscriptStatus.Error)
                    {
                        Console.WriteLine($"❌ Failed to process transcript: {fileName}");
                        ArchiveFile(filePath, "error", transcript.DetectedLanguage);
                        return;
                    }

                    var result = await _jiraTicketService!.ProcessActionItemsAsync(transcript);

                    lock (Console.Out)
                    {
                        DisplayProcessingResults(result);
                        Console.WriteLine("──────────────────────────────────────────────────────────────");
                        Console.WriteLine($"✅ File processed: {fileName}");
                        if (!_runAsWebApi) Console.Write("> ");
                    }

                    ArchiveFile(filePath, result.Success ? "success" : "error", transcript.DetectedLanguage);
                }
                finally
                {
                    _processingSemaphore?.Release();
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"⚠️  Processing cancelled for file: {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing file {fileName}: {ex.Message}");
                try
                {
                    ArchiveFile(filePath, "error");
                }
                catch (Exception archiveEx)
                {
                    Console.WriteLine($"❌ Failed to archive error file: {archiveEx.Message}");
                }
            }
            finally
            {
                _processingFiles.TryRemove(filePath, out _);
            }
        }

        private static void DisplayProcessingResults(TranscriptProcessingResult result)
        {
            Console.WriteLine($"📊 Processing Results:");
            Console.WriteLine($"   📋 Action Items Found: {result.ActionItemsFound}");
            Console.WriteLine($"   🆕 Tickets Created: {result.TicketsCreated}");
            Console.WriteLine($"   📝 Tickets Updated: {result.TicketsUpdated}");
            Console.WriteLine($"   ⏱️  Processing Time: {result.ProcessingDuration.TotalSeconds:F1}s");
            Console.WriteLine($"   ✅ Success: {(result.Success ? "Yes" : "No")}");

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                Console.WriteLine($"   ❌ Error: {result.ErrorMessage}");
            }
        }

        private static void ArchiveFile(string filePath, string status, string? languageCode = null)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                var fileName = Path.GetFileName(filePath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var languageInfo = string.IsNullOrEmpty(languageCode) ? "" : $"_{GetLanguageName(languageCode)}";
                var archivedFileName = $"{timestamp}_{status}{languageInfo}_{fileName}";
                var archivedPath = Path.Combine(ArchivePath, archivedFileName);

                File.Move(filePath, archivedPath);
                Console.WriteLine($"📦 Archived: {archivedFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Warning: Failed to archive file {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        private static void DisplayInstructions()
        {
            Console.WriteLine();
            Console.WriteLine("📚 Meeting Transcript Processor Help");
            Console.WriteLine("══════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("📁 File Processing:");
            Console.WriteLine("   • Supported formats: .txt, .md, .json, .xml, .docx, .pdf");
            Console.WriteLine("   • Place files in: " + IncomingPath);
            Console.WriteLine("   • Files are automatically processed when detected");
            Console.WriteLine();
            Console.WriteLine("⌨️  Available Commands:");
            Console.WriteLine("   status (s)   - Show system status");
            Console.WriteLine("   help (h)     - Show this help");
            Console.WriteLine("   quit (q)     - Exit application");
            Console.WriteLine();
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            _isShuttingDown = true;
            _cancellationTokenSource.Cancel();
            Console.WriteLine("\n🛑 Shutdown requested. Cleaning up...");
        }

        private static async Task CleanupAsync()
        {
            try
            {
                Console.WriteLine("🧹 Cleaning up resources...");
                _isShuttingDown = true;
                _cancellationTokenSource.Cancel();
                _fileWatcher?.Stop();

                var waitStart = DateTime.UtcNow;
                while (_processingFiles.Count > 0 && DateTime.UtcNow - waitStart < TimeSpan.FromSeconds(10))
                {
                    Console.WriteLine($"⏳ Waiting for {_processingFiles.Count} file(s) to finish processing...");
                    await Task.Delay(500);
                }

                _processingSemaphore?.Dispose();
                _cancellationTokenSource?.Dispose();

                if (_serviceProvider is IDisposable disposableProvider)
                {
                    disposableProvider.Dispose();
                }

                Console.WriteLine("✅ Cleanup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error during cleanup: {ex.Message}");
            }
        }

        private static string GetLanguageName(string languageCode)
        {
            return languageCode switch
            {
                "en" => "English",
                "fr" => "French",
                "nl" => "Dutch",
                "es" => "Spanish",
                "de" => "German",
                "pt" => "Portuguese",
                _ => "Unknown"
            };
        }
    }
}
