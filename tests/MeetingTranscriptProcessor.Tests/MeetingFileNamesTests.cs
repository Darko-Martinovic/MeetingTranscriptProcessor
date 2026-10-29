using MeetingTranscriptProcessor.Services;

namespace MeetingTranscriptProcessor.Tests;

public class MeetingFileNamesTests
{
    [Theory]
    [InlineData("standup.txt")]
    [InlineData("20260102_030405_success_Dutch_weekly sync.vtt")]
    [InlineData("notes..final.md")]
    public void A_plain_name_is_safe(string fileName)
    {
        Assert.True(MeetingFileNames.IsSafe(fileName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]
    [InlineData("../secret.txt")]
    [InlineData("..\\secret.txt")]
    [InlineData("data/secret.txt")]
    [InlineData("C:secret.txt")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("/etc/passwd")]
    [InlineData("notes.txt:stream")]
    [InlineData("what?.txt")]
    public void A_name_with_a_folder_or_a_special_character_is_not(string? fileName)
    {
        Assert.False(MeetingFileNames.IsSafe(fileName));
    }

    [Theory]
    [InlineData("standup.txt", "standup.txt")]
    [InlineData("../../standup.txt", "standup.txt")]
    [InlineData("C:\\Users\\me\\standup.txt", "standup.txt")]
    [InlineData("folder/..", null)]
    [InlineData("folder/", null)]
    public void An_upload_keeps_the_last_part_of_its_name(string clientName, string? savedName)
    {
        Assert.Equal(savedName, MeetingFileNames.FromUpload(clientName));
    }

    [Fact]
    public void A_path_stays_in_its_folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "meetings");

        Assert.Equal(Path.Combine(folder, "standup.txt"), MeetingFileNames.PathIn(folder, "standup.txt"));
        Assert.Null(MeetingFileNames.PathIn(folder, "../standup.txt"));
        Assert.Null(MeetingFileNames.PathIn(folder, ".."));
    }

    [Theory]
    [InlineData("http://localhost:5100", true)]
    [InlineData("http://127.0.0.1:5100", true)]
    [InlineData("http://[::1]:5100", true)]
    [InlineData("http://0.0.0.0:5100", false)]
    [InlineData("http://[::]:5100", false)]
    [InlineData("http://*:5100", false)]
    [InlineData("http://+:5100", false)]
    [InlineData("http://192.168.1.20:5100", false)]
    public void Only_loopback_addresses_stay_on_this_machine(string address, bool loopback)
    {
        Assert.Equal(loopback, NetworkExposure.IsLoopback(address));
    }
}
