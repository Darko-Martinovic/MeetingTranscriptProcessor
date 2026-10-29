using MeetingTranscriptProcessor.Models;

namespace MeetingTranscriptProcessor.Services;

/// <summary>
/// Reads a transcript and finds its action items: the model's, checked against the rule-based ones
/// and for hallucinations as the processing settings say, then merged
/// </summary>
public class TranscriptProcessorService : ITranscriptProcessorService
{
    private readonly TranscriptReader _reader;
    private readonly IAzureOpenAIService _aiService;
    private readonly IActionItemValidator _validator;
    private readonly IHallucinationDetector _hallucinationDetector;
    private readonly IConsistencyManager _consistencyManager;
    private readonly IConfigurationService _configService;
    private readonly SmartTitleGeneratorService _titleGenerator;
    private readonly ILogger<TranscriptProcessorService> _logger;

    public TranscriptProcessorService(
        TranscriptReader reader,
        IAzureOpenAIService aiService,
        IActionItemValidator validator,
        IHallucinationDetector hallucinationDetector,
        IConsistencyManager consistencyManager,
        IConfigurationService configService,
        SmartTitleGeneratorService titleGenerator,
        ILogger<TranscriptProcessorService> logger)
    {
        _reader = reader;
        _aiService = aiService;
        _validator = validator;
        _hallucinationDetector = hallucinationDetector;
        _consistencyManager = consistencyManager;
        _configService = configService;
        _titleGenerator = titleGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Processes a transcript file and extracts action items
    /// </summary>
    public async Task<MeetingTranscript> ProcessTranscriptAsync(string filePath, double? temperature = null)
    {
        try
        {
            var transcript = await _reader.ReadAsync(filePath);
            transcript.Status = TranscriptStatus.Processing;
            transcript.ProcessedAt = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(transcript.Content))
            {
                throw new InvalidOperationException("Transcript content is empty");
            }

            await ExtractActionItemsAsync(transcript, temperature);
            _titleGenerator.UpdateTitleIfGeneric(transcript);
            transcript.Status = TranscriptStatus.Processed;

            _logger.LogInformation(
                "Processed {FileName}: {Count} action items", transcript.FileName, transcript.ActionItems.Count);
            return transcript;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transcript {FileName}", Path.GetFileName(filePath));
            return new MeetingTranscript
            {
                FileName = Path.GetFileName(filePath),
                Status = TranscriptStatus.Error,
                ProcessedAt = DateTime.UtcNow
            };
        }
    }

    private async Task ExtractActionItemsAsync(MeetingTranscript transcript, double? temperature)
    {
        var settings = _configService.GetProcessingSettings();
        var extractionSettings = _configService.GetExtractionSettings();

        try
        {
            transcript.DetectedLanguage = _consistencyManager.CreateExtractionConfiguration(transcript).Language;

            // The consistency manager adds the meeting type and language to the prompt
            ConsistencyContext? consistencyContext = null;
            string prompt;
            if (settings.EnableConsistencyManagement)
            {
                consistencyContext = _consistencyManager.CreateConsistencyContext(transcript);
                prompt = _consistencyManager.GenerateContextualPrompt(transcript, consistencyContext);
            }
            else
            {
                prompt = _configService.GetExtractionPrompt(transcript);
            }

            var aiResponse = await _aiService.ProcessTranscriptAsync(prompt, temperature);
            transcript.TokensUsed = aiResponse.TokensUsed;
            transcript.EstimatedCost = aiResponse.EstimatedCost;

            // An answer that isn't the JSON asked for is read line by line, like a transcript
            var aiExtracted = AiResponseParser.Parse(aiResponse.Content, transcript.ProjectKey)
                ?? RuleBasedExtractor.Extract(aiResponse.Content, transcript.ProjectKey, new ExtractionSettings());

            if (settings.EnableValidation)
            {
                var ruleBased = RuleBasedExtractor.Extract(transcript.Content, transcript.ProjectKey, extractionSettings);
                var validation = _validator.ValidateActionItems(aiExtracted, ruleBased, transcript);
                _logger.LogInformation(
                    "Validation of {FileName}: {Ai} from the model, {Rules} from rules, cross-validation {Score:F2}, confidence {Confidence:F2}",
                    transcript.FileName, aiExtracted.Count, ruleBased.Count,
                    validation.CrossValidationScore, validation.OverallConfidence);
                foreach (var warning in validation.PotentialFalsePositives.Concat(validation.PotentialFalseNegatives))
                {
                    _logger.LogDebug("{FileName}: {Warning}", transcript.FileName, warning);
                }
            }

            var kept = aiExtracted;
            if (settings.EnableHallucinationDetection)
            {
                var analysis = _hallucinationDetector.AnalyzeActionItems(aiExtracted, transcript);
                var threshold = consistencyContext?.ConfidenceThreshold ?? settings.ValidationConfidenceThreshold;
                kept = _hallucinationDetector.FilterHighConfidenceItems(aiExtracted, transcript, threshold);
                _logger.LogInformation(
                    "Hallucination check of {FileName}: rate {Rate:P0}, {Dropped} of {Count} items dropped",
                    transcript.FileName, analysis.HallucinationRate, aiExtracted.Count - kept.Count, aiExtracted.Count);
            }

            transcript.ActionItems = ActionItemMerger.Deduplicate(ActionItemMerger.ApplyBusinessRules(kept));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction with the model failed for {FileName}; using rules", transcript.FileName);
            transcript.ActionItems = RuleBasedExtractor.Extract(transcript.Content, transcript.ProjectKey, extractionSettings);
        }
    }
}
