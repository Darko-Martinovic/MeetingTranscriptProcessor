using MeetingTranscriptProcessor.Services;
using MeetingTranscriptProcessor.Models;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

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

        // Concurrency control
        private static SemaphoreSlim? _processingSemaphore;
        private static readonly ConcurrentDictionary<string, bool> _processingFiles = new();
        private static readonly CancellationTokenSource _cancellationTokenSource = new();
        private static int _maxConcurrentFiles = 3; // Default value, configurable via environment

        // Validation service control - configurable via environment
        private static bool _enableValidation = true;
        private static bool _enableHallucinationDetection = true;
        private static bool _enableConsistencyManagement = true;
        private static double _validationConfidenceThreshold = 0.5;

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

                // Configure concurrency settings
                var maxConcurrentEnv = Environment.GetEnvironmentVariable("MAX_CONCURRENT_FILES");
                if (
                    int.TryParse(maxConcurrentEnv, out var maxConcurrent)
                    && maxConcurrent > 0
                    && maxConcurrent <= 10
                )
                {
                    _maxConcurrentFiles = maxConcurrent;
                }
                Console.WriteLine($"📊 Max concurrent file processing: {_maxConcurrentFiles}");

                // Configure validation service settings
                var enableValidationEnv = Environment.GetEnvironmentVariable("ENABLE_VALIDATION");
                if (bool.TryParse(enableValidationEnv, out var enableValidation))
                {
                    _enableValidation = enableValidation;
                }

                var enableHallucinationEnv = Environment.GetEnvironmentVariable(
                    "ENABLE_HALLUCINATION_DETECTION"
                );
                if (bool.TryParse(enableHallucinationEnv, out var enableHallucination))
                {
                    _enableHallucinationDetection = enableHallucination;
                }

                var enableConsistencyEnv = Environment.GetEnvironmentVariable(
                    "ENABLE_CONSISTENCY_MANAGEMENT"
                );
                if (bool.TryParse(enableConsistencyEnv, out var enableConsistency))
                {
                    _enableConsistencyManagement = enableConsistency;
                }

                var thresholdEnv = Environment.GetEnvironmentVariable(
                    "VALIDATION_CONFIDENCE_THRESHOLD"
                );
                if (
                    double.TryParse(thresholdEnv, out var threshold)
                    && threshold >= 0.0
                    && threshold <= 1.0
                )
                {
                    _validationConfidenceThreshold = threshold;
                }

                Console.WriteLine($"🔍 Validation enabled: {_enableValidation}");
                Console.WriteLine($"🧠 Hallucination detection: {_enableHallucinationDetection}");
                Console.WriteLine($"🎯 Consistency management: {_enableConsistencyManagement}");
                Console.WriteLine($"📊 Validation threshold: {_validationConfidenceThreshold:F1}");
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

            // Initialize concurrency semaphore
            _processingSemaphore = new SemaphoreSlim(_maxConcurrentFiles, _maxConcurrentFiles);

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

            // Register configuration service first (other services depend on it)
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

            // Build service provider
            _serviceProvider = services.BuildServiceProvider();

            // Get services
            _fileWatcher = _serviceProvider.GetRequiredService<IFileWatcherService>();
            _transcriptProcessor =
                _serviceProvider.GetRequiredService<ITranscriptProcessorService>();
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

            // Validation services status
            Console.WriteLine(
                $"🔍 Validation: {(_enableValidation ? "✅ Enabled" : "⚠️  Disabled")}"
            );
            Console.WriteLine(
                $"🧠 Hallucination Detection: {(_enableHallucinationDetection ? "✅ Enabled" : "⚠️  Disabled")}"
            );
            Console.WriteLine(
                $"🎯 Consistency Management: {(_enableConsistencyManagement ? "✅ Enabled" : "⚠️  Disabled")}"
            );
            Console.WriteLine($"📊 Validation Threshold: {_validationConfidenceThreshold:F1}");

            // Concurrency status
            var availableSlots = _processingSemaphore?.CurrentCount ?? 0;
            var usedSlots = _maxConcurrentFiles - availableSlots;
            var filesBeingProcessed = _processingFiles.Count;

            Console.WriteLine(
                $"⚡ Concurrency: {usedSlots}/{_maxConcurrentFiles} slots used, {filesBeingProcessed} files processing"
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
            Console.WriteLine("5. Up to 3 files can be processed concurrently");
            Console.WriteLine();
            Console.WriteLine("⌨️  Commands:");
            Console.WriteLine("   'status' - Show current status");
            Console.WriteLine("   'metrics' - Show AI validation metrics");
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

                        case "metrics":
                        case "m":
                            DisplayValidationMetrics();
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
        /// Handles file detection events with concurrent processing support
        /// </summary>
        private static async void OnFileDetected(object? sender, FileDetectedEventArgs e)
        {
            // Fire and forget pattern to allow concurrent processing
            await Task.Run(async () =>
            {
                await ProcessFileAsync(e.FilePath, e.FileName);
            });
        }

        /// <summary>
        /// Processes a single file with concurrency control
        /// </summary>
        private static async Task ProcessFileAsync(string filePath, string fileName)
        {
            // Check if we're shutting down
            if (_isShuttingDown || _cancellationTokenSource.Token.IsCancellationRequested)
                return;

            // Check if this file is already being processed
            if (!_processingFiles.TryAdd(filePath, true))
            {
                Console.WriteLine($"⚠️  File already being processed: {fileName}");
                return;
            }

            try
            {
                // Wait for available processing slot
                if (_processingSemaphore != null)
                {
                    await _processingSemaphore.WaitAsync(_cancellationTokenSource.Token);
                }

                try
                {
                    // Double-check if we're still not shutting down
                    if (_isShuttingDown || _cancellationTokenSource.Token.IsCancellationRequested)
                        return;

                    Console.WriteLine(
                        $"\n📄 Processing file: {fileName} (Thread: {Thread.CurrentThread.ManagedThreadId})"
                    );
                    Console.WriteLine(
                        "──────────────────────────────────────────────────────────────"
                    );

                    // Process the transcript
                    var transcript = await _transcriptProcessor!.ProcessTranscriptAsync(filePath);

                    if (transcript.Status == TranscriptStatus.Error)
                    {
                        Console.WriteLine($"❌ Failed to process transcript: {fileName}");
                        ArchiveFile(filePath, "error", transcript.DetectedLanguage);
                        return;
                    }

                    // Process action items and create Jira tickets
                    var result = await _jiraTicketService!.ProcessActionItemsAsync(transcript);

                    // Display results (thread-safe console output)
                    lock (Console.Out)
                    {
                        DisplayProcessingResults(result);
                        Console.WriteLine(
                            "──────────────────────────────────────────────────────────────"
                        );
                        Console.WriteLine(
                            $"✅ File processed: {fileName} (Thread: {Thread.CurrentThread.ManagedThreadId})"
                        );
                        Console.Write("> ");
                    }

                    // Archive the processed file
                    ArchiveFile(
                        filePath,
                        result.Success ? "success" : "error",
                        transcript.DetectedLanguage
                    );
                }
                finally
                {
                    // Release the processing slot
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
                    ArchiveFile(filePath, "error"); // No language info available in error case
                }
                catch (Exception archiveEx)
                {
                    Console.WriteLine($"❌ Failed to archive error file: {archiveEx.Message}");
                }
            }
            finally
            {
                // Remove from processing dictionary
                _processingFiles.TryRemove(filePath, out _);
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
        /// Archives a processed file with language information
        /// </summary>
        private static void ArchiveFile(string filePath, string status, string? languageCode = null)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                var fileName = Path.GetFileName(filePath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                // Include language in filename if available
                var languageInfo = string.IsNullOrEmpty(languageCode)
                    ? ""
                    : $"_{GetLanguageName(languageCode)}";
                var archivedFileName = $"{timestamp}_{status}{languageInfo}_{fileName}";
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
            Console.WriteLine("   • Up to 3 files can be processed concurrently");
            Console.WriteLine("   • Duplicate files are automatically handled");
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
            Console.WriteLine("🔍 AI/ML Validation Features:");
            Console.WriteLine(
                $"   • Validation: {(_enableValidation ? "✅ Enabled" : "⚠️  Disabled")} (ENABLE_VALIDATION)"
            );
            Console.WriteLine(
                $"   • Hallucination Detection: {(_enableHallucinationDetection ? "✅ Enabled" : "⚠️  Disabled")} (ENABLE_HALLUCINATION_DETECTION)"
            );
            Console.WriteLine(
                $"   • Consistency Management: {(_enableConsistencyManagement ? "✅ Enabled" : "⚠️  Disabled")} (ENABLE_CONSISTENCY_MANAGEMENT)"
            );
            Console.WriteLine(
                $"   • Confidence Threshold: {_validationConfidenceThreshold:F1} (VALIDATION_CONFIDENCE_THRESHOLD)"
            );
            Console.WriteLine(
                "   • Set these environment variables to false to temporarily disable features"
            );
            Console.WriteLine();
            Console.WriteLine("⚡ Concurrency Features:");
            Console.WriteLine(
                $"   • Parallel processing of up to {_maxConcurrentFiles} files simultaneously"
            );
            Console.WriteLine("   • Thread-safe console output with thread ID tracking");
            Console.WriteLine("   • Graceful shutdown waits for ongoing operations");
            Console.WriteLine("   • Duplicate file detection prevents double processing");
            Console.WriteLine("   • Configurable via MAX_CONCURRENT_FILES environment variable");
            Console.WriteLine();
            Console.WriteLine("⌨️  Available Commands:");
            Console.WriteLine("   status (s)   - Show system status");
            Console.WriteLine("   metrics (m)  - Show AI validation metrics");
            Console.WriteLine("   help (h)     - Show this help");
            Console.WriteLine("   quit (q)     - Exit application");
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
        /// <summary>
        /// Handles Ctrl+C cancellation requests
        /// </summary>
        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            _isShuttingDown = true;
            _cancellationTokenSource.Cancel();
            Console.WriteLine("\n🛑 Shutdown requested. Cleaning up...");
        }

        /// <summary>
        /// Cleans up resources on shutdown
        /// </summary>
        /// <summary>
        /// Cleanup application resources
        /// </summary>
        private static async Task CleanupAsync()
        {
            try
            {
                Console.WriteLine("🧹 Cleaning up resources...");

                // Signal shutdown and cancel any ongoing operations
                _isShuttingDown = true;
                _cancellationTokenSource.Cancel();

                // Stop file watcher first
                _fileWatcher?.Stop();

                // Wait for any ongoing file processing to complete (with timeout)
                var waitStart = DateTime.UtcNow;
                while (
                    _processingFiles.Count > 0
                    && DateTime.UtcNow - waitStart < TimeSpan.FromSeconds(10)
                )
                {
                    Console.WriteLine(
                        $"⏳ Waiting for {_processingFiles.Count} file(s) to finish processing..."
                    );
                    await Task.Delay(500);
                }

                if (_processingFiles.Count > 0)
                {
                    Console.WriteLine(
                        $"⚠️  Force closing with {_processingFiles.Count} file(s) still processing"
                    );
                }

                // Dispose concurrency resources
                _processingSemaphore?.Dispose();
                _cancellationTokenSource?.Dispose();

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
        }

        /// <summary>
        /// Display validation metrics for monitoring AI/ML reliability
        /// </summary>
        private static void DisplayValidationMetrics()
        {
            try
            {
                var metrics = ActionItemValidator.GetValidationMetrics();

                if (metrics.TotalValidations == 0)
                {
                    Console.WriteLine("📊 No validation metrics available yet");
                    return;
                }

                Console.WriteLine("📊 Validation Metrics Summary:");
                Console.WriteLine($"   Total validations: {metrics.TotalValidations}");
                Console.WriteLine($"   Average confidence: {metrics.AverageConfidence:P}");
                Console.WriteLine(
                    $"   Cross-validation score: {metrics.AverageCrossValidationScore:P}"
                );
                Console.WriteLine($"   Context coherence: {metrics.AverageContextCoherence:P}");
                Console.WriteLine($"   High confidence rate: {metrics.HighConfidenceRate:P}");
                Console.WriteLine($"   Low confidence rate: {metrics.LowConfidenceRate:P}");
                Console.WriteLine($"   Total false positives: {metrics.TotalFalsePositives}");
                Console.WriteLine($"   Total false negatives: {metrics.TotalFalseNegatives}");

                // Calculate and display false positive/negative rates
                if (metrics.TotalValidations > 0)
                {
                    var avgItemsPerValidation = 5.0; // Estimate, could be tracked more precisely
                    var totalItems = metrics.TotalValidations * avgItemsPerValidation;
                    var falsePositiveRate = metrics.TotalFalsePositives / totalItems;
                    var falseNegativeRate = metrics.TotalFalseNegatives / totalItems;

                    Console.WriteLine($"   Estimated false positive rate: {falsePositiveRate:P}");
                    Console.WriteLine($"   Estimated false negative rate: {falseNegativeRate:P}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error displaying validation metrics: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts language code to readable name for archiving
        /// </summary>
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
