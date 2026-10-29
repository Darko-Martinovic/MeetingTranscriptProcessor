using MeetingTranscriptProcessor.Models;
using MeetingTranscriptProcessor.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MeetingTranscriptProcessor.Tests;

/// <summary>
/// Each file format the app accepts, read from the sample files in data/Templates
/// </summary>
public class TranscriptReadingTests(AppFactory app) : IClassFixture<AppFactory>
{
    private async Task<MeetingTranscript> ProcessAsync(string template)
    {
        var copy = Path.Combine(app.Root, template);
        File.Copy(Repository.Template(template), copy, overwrite: true);
        return await app.Services.GetRequiredService<ITranscriptProcessorService>().ProcessTranscriptAsync(copy);
    }

    [Fact]
    public async Task A_text_file_gives_its_title_date_and_participants()
    {
        var transcript = await ProcessAsync("sample_meeting_transcript.txt");

        Assert.Equal(TranscriptStatus.Processed, transcript.Status);
        Assert.Equal("Weekly Team Standup - Project Phoenix", transcript.Title);
        Assert.Equal(new DateTime(2024, 1, 15), transcript.MeetingDate);
        Assert.Equal(["Alice Johnson", "Bob Smith", "Carol Davis", "David Wilson"], transcript.Participants);
        Assert.Equal("en", transcript.DetectedLanguage);
    }

    [Theory]
    [InlineData("transcript_nl.vtt", "nl")]
    [InlineData("transcript_fr.vtt", "fr")]
    [InlineData("integration_observability_review.vtt", "en")]
    public async Task A_webvtt_file_is_read_as_speaker_lines(string template, string language)
    {
        var transcript = await ProcessAsync(template);

        Assert.Equal(TranscriptStatus.Processed, transcript.Status);
        Assert.DoesNotContain("WEBVTT", transcript.Content);
        Assert.DoesNotContain("-->", transcript.Content);
        Assert.Contains("John", transcript.Participants);
        Assert.Equal(language, transcript.DetectedLanguage);
    }

    [Theory]
    [InlineData("transcript_nl.docx", "nl")]
    [InlineData("transcript_fr.docx", "fr")]
    [InlineData("teams_transcript_exact.docx", "en")]
    public async Task A_word_file_is_read_as_text(string template, string language)
    {
        var transcript = await ProcessAsync(template);

        Assert.Equal(TranscriptStatus.Processed, transcript.Status);
        Assert.DoesNotContain("[Error reading DOCX file", transcript.Content);
        Assert.NotEmpty(transcript.Participants);
        Assert.Equal(language, transcript.DetectedLanguage);
    }

    [Fact]
    public async Task A_teams_json_file_is_read_as_speaker_lines()
    {
        var transcript = await ProcessAsync("ms_graph_style_transcript_en.json");

        Assert.Equal(TranscriptStatus.Processed, transcript.Status);
        Assert.StartsWith("MEETING TRANSCRIPT", transcript.Content);
        Assert.Contains("Title: Integration and Observability Review", transcript.Content);
        Assert.Contains("John: ", transcript.Content);
        Assert.DoesNotContain("Recording started.", transcript.Content);
        Assert.Equal("Integration and Observability Review", transcript.Title);
        Assert.Equal(new DateTime(2025, 10, 17), transcript.MeetingDate);
        Assert.Contains("Mike", transcript.Participants);
    }

    [Fact]
    public async Task A_pdf_file_is_read_as_text()
    {
        var transcript = await ProcessAsync("sample_meeting_transcript.pdf");

        Assert.Equal(TranscriptStatus.Processed, transcript.Status);
        Assert.Equal("Weekly Team Standup - Project Phoenix", transcript.Title);
        Assert.Equal(new DateTime(2024, 1, 15), transcript.MeetingDate);
        Assert.Equal(["Alice Johnson", "Bob Smith", "Carol Davis", "David Wilson"], transcript.Participants);
        Assert.Contains("password reset", transcript.Content);
    }

    [Fact]
    public async Task Without_a_model_the_rules_find_the_action_items()
    {
        var transcript = await ProcessAsync("sample_meeting_transcript.txt");

        Assert.NotEmpty(transcript.ActionItems);
        Assert.Contains(transcript.ActionItems, item => item.Title.Contains("password reset", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, transcript.TokensUsed ?? 0);
    }

    [Fact]
    public async Task An_empty_file_is_an_error()
    {
        var empty = Path.Combine(app.Root, "empty.txt");
        await File.WriteAllTextAsync(empty, "", TestContext.Current.CancellationToken);

        var transcript = await app.Services.GetRequiredService<ITranscriptProcessorService>().ProcessTranscriptAsync(empty);

        Assert.Equal(TranscriptStatus.Error, transcript.Status);
    }
}
