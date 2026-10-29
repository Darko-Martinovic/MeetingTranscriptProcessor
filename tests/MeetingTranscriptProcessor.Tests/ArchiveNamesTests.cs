using MeetingTranscriptProcessor.Services;

namespace MeetingTranscriptProcessor.Tests;

public class ArchiveNamesTests
{
    [Fact]
    public void An_archived_name_has_the_time_status_and_language_before_the_file_name()
    {
        var name = ArchiveNames.ArchivedName("standup.txt", "success", "nl", new DateTime(2026, 1, 2, 3, 4, 5));

        Assert.Equal("20260102_030405_success_Dutch_standup.txt", name);
    }

    [Theory]
    [InlineData("20260102_030405_success_Dutch_standup.txt", "standup")]
    [InlineData("20260102_030405_error_standup.txt", "standup")]
    [InlineData("20260102_030405_success_English_20260102_030000_weekly_sync.vtt", "20260102_030000_weekly_sync")]
    [InlineData("weekly_sync.txt", "weekly_sync")]
    [InlineData("2026_01_02_notes.txt", "2026_01_02_notes")]
    public void The_base_name_drops_what_archiving_added(string fileName, string baseName)
    {
        Assert.Equal(baseName, ArchiveNames.BaseName(fileName));
    }

    [Fact]
    public void A_metadata_name_is_the_base_name_with_meta_json()
    {
        Assert.Equal("standup.meta.json", ArchiveNames.MetadataName("20260102_030405_success_Dutch_standup.txt"));
    }
}
