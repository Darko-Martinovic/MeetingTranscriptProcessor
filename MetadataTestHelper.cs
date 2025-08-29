using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeetingTranscriptProcessor.Test
{
    /// <summary>
    /// Test helper for metadata serialization functionality.
    /// This class is used for development and testing purposes only.
    /// It is not called during normal application execution.
    /// </summary>
    public static class MetadataTestHelper
    {
        public static async Task TestMetadataSaving()
        {
            Console.WriteLine("🧪 Starting metadata saving test...");

            try
            {
                // Create a test transcript with JIRA tickets
                var transcript = new Models.MeetingTranscript
                {
                    Id = Guid.NewGuid().ToString(),
                    FileName = "test_metadata.txt",
                    Title = "Test Meeting",
                    Content = "Test content",
                    ProcessedAt = DateTime.UtcNow,
                    DetectedLanguage = "en"
                };

                // Add test action items
                transcript.ActionItems.Add(new Models.ActionItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "Test Action Item",
                    Description = "This is a test action item",
                    Priority = Models.ActionItemPriority.Medium,
                    Type = Models.ActionItemType.Task
                });

                // Add test JIRA ticket
                transcript.CreatedJiraTickets.Add(new Models.JiraTicketReference
                {
                    TicketKey = "TEST-123",
                    TicketUrl = "https://test.atlassian.net/browse/TEST-123",
                    Title = "Test Ticket",
                    ActionItemId = transcript.ActionItems[0].Id,
                    CreatedAt = DateTime.UtcNow,
                    Priority = "Medium",
                    Type = "Task",
                    Status = "Open"
                });

                Console.WriteLine($"📊 Test transcript has {transcript.CreatedJiraTickets.Count} JIRA tickets");

                // Test file path
                var testFilePath = Path.Combine("d:\\DotNetOpenAI\\MeetingTranscriptProcessor\\data\\Processing", "test_metadata.txt");
                var metadataPath = Path.Combine("d:\\DotNetOpenAI\\MeetingTranscriptProcessor\\data\\Processing", "test_metadata.meta.json");

                // Create test file
                await File.WriteAllTextAsync(testFilePath, "Test content");

                // Test JSON serialization first
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var jsonContent = JsonSerializer.Serialize(transcript, options);
                Console.WriteLine($"📄 JSON serialization successful, length: {jsonContent.Length}");
                Console.WriteLine($"🎫 JIRA tickets in JSON: Contains 'TEST-123': {jsonContent.Contains("TEST-123")}");

                // Test file writing
                await File.WriteAllTextAsync(metadataPath, jsonContent);
                Console.WriteLine($"✅ Metadata file written to: {metadataPath}");

                // Verify file exists and read it back
                if (File.Exists(metadataPath))
                {
                    var fileInfo = new FileInfo(metadataPath);
                    Console.WriteLine($"✅ File verified - Size: {fileInfo.Length} bytes");

                    var readBack = await File.ReadAllTextAsync(metadataPath);
                    var deserializedTranscript = JsonSerializer.Deserialize<Models.MeetingTranscript>(readBack, options);
                    Console.WriteLine($"✅ Deserialization successful - JIRA tickets: {deserializedTranscript?.CreatedJiraTickets.Count ?? 0}");
                }
                else
                {
                    Console.WriteLine("❌ Metadata file was not created");
                }

                Console.WriteLine("🧪 Metadata saving test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            }
        }
    }
}
