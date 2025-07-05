using MeetingTranscriptProcessor.Services;
using MeetingTranscriptProcessor.Models;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeetingTranscriptProcessor
{
    /// <summary>
    /// Main application for processing meeting transcripts and creating Jira tickets
    /// </summary>
    internal class Program
    {
        private static IServiceProvider? _serviceProvider;
        private static IFileWatcherService? _fileWatcher;
        private static ITranscriptProcessorService? _transcriptProcessor;
        private static IJiraTicketService? _jiraTicketService;
        private static bool _isShuttingDown = false;

        // Directory paths - will be updated after loading environment
        private static string DataPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Data"
        );
        private static string IncomingPath = "";
        private static string ProcessingPath = "";
        private static string ArchivePath = "";

        static async Task Main(string[] args)
        {
            try
            {
                // Display application header
                DisplayHeader();

                // Load environment variables
                LoadEnvironment();

                // Initialize services
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

        /// <summary>
        /// Displays the application header
        /// </summary>
        private static void DisplayHeader()
        {
            Console.Clear();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║               Meeting Transcript Processor                  ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  Automatically processes meeting transcripts and creates    ║");
            Console.WriteLine("║  Jira tickets from extracted action items.                  ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }

        /// <summary>
        /// Loads environment variables from .env file
        /// </summary>
        private static void LoadEnvironment()
        {
            try
            {
                // Try to load .env file from project root, not from the bin directory
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var projectRoot =
                    Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName ?? baseDir;
                var envPath = Path.Combine(projectRoot, ".env");

                if (File.Exists(envPath))
                {
                    Env.Load(envPath);
                    Console.WriteLine("✅ Environment configuration loaded from project root");
                }
                else
                {
                    // Fallback: try current directory
                    envPath = Path.Combine(baseDir, ".env");
                    if (File.Exists(envPath))
                    {
                        Env.Load(envPath);
                        Console.WriteLine("✅ Environment configuration loaded");
                    }
                    else
                    {
                        Console.WriteLine(
                            "ℹ️  No .env file found - using system environment variables"
                        );
                    }
                }

                // Set directory paths from environment or use defaults
                var incomingEnv = Environment.GetEnvironmentVariable("INCOMING_DIRECTORY");
                var processingEnv = Environment.GetEnvironmentVariable("PROCESSING_DIRECTORY");
                var archiveEnv = Environment.GetEnvironmentVariable("ARCHIVE_DIRECTORY");

                IncomingPath =
                    !string.IsNullOrEmpty(incomingEnv) && Path.IsPathRooted(incomingEnv)
                        ? incomingEnv
                        : Path.Combine(projectRoot, incomingEnv ?? "Data\\Incoming");

                ProcessingPath =
                    !string.IsNullOrEmpty(processingEnv) && Path.IsPathRooted(processingEnv)
                        ? processingEnv
                        : Path.Combine(projectRoot, processingEnv ?? "Data\\Processing");

                ArchivePath =
                    !string.IsNullOrEmpty(archiveEnv) && Path.IsPathRooted(archiveEnv)
                        ? archiveEnv
                        : Path.Combine(projectRoot, archiveEnv ?? "Data\\Archive");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Warning: Could not load environment: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes all services
        /// </summary>
        private static void InitializeServices()
        {
            Console.WriteLine("🔧 Initializing services...");

            // Ensure directories exist
            Directory.CreateDirectory(IncomingPath);
            Directory.CreateDirectory(ProcessingPath);
            Directory.CreateDirectory(ArchivePath);

            // Configure dependency injection
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder => builder.AddConsole());

            // Register custom logger wrapper
            services.AddSingleton<MeetingTranscriptProcessor.Services.ILogger, ConsoleLogger>();

            // Register services
            services.AddSingleton<IAzureOpenAIService, AzureOpenAIService>();
            services.AddSingleton<ITranscriptProcessorService, TranscriptProcessorService>();
            services.AddSingleton<IJiraTicketService, JiraTicketService>();
            services.AddSingleton<IFileWatcherService>(provider =>
                new FileWatcherService(IncomingPath, ProcessingPath, provider.GetService<MeetingTranscriptProcessor.Services.ILogger>()));

            // Build service provider
            _serviceProvider = services.BuildServiceProvider();

            // Get services
            _fileWatcher = _serviceProvider.GetRequiredService<IFileWatcherService>();
            _transcriptProcessor = _serviceProvider.GetRequiredService<ITranscriptProcessorService>();
            _jiraTicketService = _serviceProvider.GetRequiredService<IJiraTicketService>();

            // Setup event handlers
            _fileWatcher.FileDetected += OnFileDetected;

            Console.WriteLine("✅ Services initialized successfully");
        }

        /// <summary>
        /// Starts the file watcher
        /// </summary>
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

        /// <summary>
        /// Displays current status and configuration
        /// </summary>
        private static void DisplayStatus()
        {
            Console.WriteLine();
            Console.WriteLine("📊 System Status:");
            Console.WriteLine("──────────────────────────────────────────────────────────────");

            // Directory status
            Console.WriteLine($"📁 Monitoring: {IncomingPath}");
            Console.WriteLine($"📁 Processing: {ProcessingPath}");
            Console.WriteLine($"📁 Archive: {ArchivePath}");

            // Service status
            Console.WriteLine(
                $"🤖 Azure OpenAI: {(_serviceProvider?.GetService<IAzureOpenAIService>()?.IsConfigured() == true ? "✅ Configured" : "⚠️  Not configured (using fallback)")}"
            );
            Console.WriteLine(
                $"🎫 Jira Integration: {(IsJiraConfigured() ? "✅ Configured" : "⚠️  Not configured (simulation mode)")}"
            );

            Console.WriteLine("──────────────────────────────────────────────────────────────");
            Console.WriteLine();
            Console.WriteLine("📝 Instructions:");
            Console.WriteLine(
                "1. Place transcript files (.txt, .md, .json, .xml, .docx, .pdf) in the Incoming folder"
            );
            Console.WriteLine(
                "2. Files will be automatically processed and moved to Processing folder"
            );
            Console.WriteLine("3. Action items will be extracted and Jira tickets created/updated");
            Console.WriteLine("4. Processed files will be archived");
            Console.WriteLine();
            Console.WriteLine("⌨️  Commands:");
            Console.WriteLine("   'status' - Show current status");
            Console.WriteLine("   'help' - Show this help message");
            Console.WriteLine("   'quit' or Ctrl+C - Exit application");
            Console.WriteLine();
            Console.WriteLine("🟢 System is running and monitoring for files...");
            Console.WriteLine();
        }

        /// <summary>
        /// Runs the main application loop
        /// </summary>
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
                            // Empty input, continue
                            break;

                        default:
                            Console.WriteLine(
                                $"Unknown command: '{input}'. Type 'help' for available commands."
                            );
                            break;
                    }

                    await Task.Delay(100); // Small delay to prevent CPU spinning
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error in main loop: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles file detection events
        /// </summary>
        private static async void OnFileDetected(object? sender, FileDetectedEventArgs e)
        {
            try
            {
                if (_isShuttingDown)
                    return;

                Console.WriteLine($"\n📄 Processing file: {e.FileName}");
                Console.WriteLine("──────────────────────────────────────────────────────────────");

                // Process the transcript
                var transcript = await _transcriptProcessor!.ProcessTranscriptAsync(e.FilePath);

                if (transcript.Status == TranscriptStatus.Error)
                {
                    Console.WriteLine($"❌ Failed to process transcript: {e.FileName}");
                    ArchiveFile(e.FilePath, "error");
                    return;
                }

                // Process action items and create Jira tickets
                var result = await _jiraTicketService!.ProcessActionItemsAsync(transcript);

                // Display results
                DisplayProcessingResults(result);

                // Archive the processed file
                ArchiveFile(e.FilePath, result.Success ? "success" : "error");

                Console.WriteLine("──────────────────────────────────────────────────────────────");
                Console.WriteLine("🟢 Ready for next file...");
                Console.Write("> ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing file {e.FileName}: {ex.Message}");

                try
                {
                    ArchiveFile(e.FilePath, "error");
                }
                catch (Exception archiveEx)
                {
                    Console.WriteLine($"❌ Failed to archive error file: {archiveEx.Message}");
                }
            }
        }

        /// <summary>
        /// Displays processing results
        /// </summary>
        private static void DisplayProcessingResults(TranscriptProcessingResult result)
        {
            Console.WriteLine($"📊 Processing Results:");
            Console.WriteLine($"   📋 Action Items Found: {result.ActionItemsFound}");
            Console.WriteLine($"   🆕 Tickets Created: {result.TicketsCreated}");
            Console.WriteLine($"   📝 Tickets Updated: {result.TicketsUpdated}");
            Console.WriteLine(
                $"   ⏱️  Processing Time: {result.ProcessingDuration.TotalSeconds:F1}s"
            );
            Console.WriteLine($"   ✅ Success: {(result.Success ? "Yes" : "No")}");

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                Console.WriteLine($"   ❌ Error: {result.ErrorMessage}");
            }

            // Display individual ticket results
            if (result.TicketResults.Any())
            {
                Console.WriteLine($"   🎫 Ticket Details:");
                foreach (var ticketResult in result.TicketResults)
                {
                    var status = ticketResult.Success ? "✅" : "❌";
                    var operation = ticketResult.Operation.ToString().ToUpper();
                    Console.WriteLine(
                        $"      {status} {operation}: {ticketResult.TicketKey} - {ticketResult.Message}"
                    );
                }
            }
        }

        /// <summary>
        /// Archives a processed file
        /// </summary>
        private static void ArchiveFile(string filePath, string status)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                var fileName = Path.GetFileName(filePath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var archivedFileName = $"{timestamp}_{status}_{fileName}";
                var archivedPath = Path.Combine(ArchivePath, archivedFileName);

                // Move file to archive
                File.Move(filePath, archivedPath);

                Console.WriteLine($"📦 Archived: {archivedFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"⚠️  Warning: Failed to archive file {Path.GetFileName(filePath)}: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Displays help instructions
        /// </summary>
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
            Console.WriteLine("🎫 Jira Integration:");
            Console.WriteLine(
                "   • Set JIRA_URL, JIRA_API_TOKEN, JIRA_EMAIL environment variables"
            );
            Console.WriteLine("   • Optional: Set JIRA_DEFAULT_PROJECT (default: TASK)");
            Console.WriteLine("   • Without configuration, operates in simulation mode");
            Console.WriteLine();
            Console.WriteLine("🤖 Azure OpenAI Integration:");
            Console.WriteLine(
                "   • Set AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY environment variables"
            );
            Console.WriteLine("   • Optional: Set AZURE_OPENAI_DEPLOYMENT_NAME (default: gpt-4)");
            Console.WriteLine("   • Without configuration, uses rule-based extraction");
            Console.WriteLine();
            Console.WriteLine("⌨️  Available Commands:");
            Console.WriteLine("   status (s)  - Show system status");
            Console.WriteLine("   help (h)    - Show this help");
            Console.WriteLine("   quit (q)    - Exit application");
            Console.WriteLine();
        }

        /// <summary>
        /// Checks if Jira is configured
        /// </summary>
        private static bool IsJiraConfigured()
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JIRA_URL"))
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JIRA_API_TOKEN"))
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JIRA_EMAIL"));
        }

        /// <summary>
        /// Handles Ctrl+C graceful shutdown
        /// </summary>
        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            _isShuttingDown = true;
            Console.WriteLine("\n🛑 Shutdown requested. Cleaning up...");
        }

        /// <summary>
        /// Cleans up resources on shutdown
        /// </summary>
        private static Task CleanupAsync()
        {
            try
            {
                Console.WriteLine("🧹 Cleaning up resources...");

                _fileWatcher?.Stop();

                // Dispose service provider (this will dispose all registered services)
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

            return Task.CompletedTask;
        }
    }
}
