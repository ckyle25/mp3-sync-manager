using Mp3SyncManager.Models;

namespace Mp3SyncManager.Tests;

public class MusicFileTests
{
    [Theory]
    [InlineData(0, "< 1 KB")]
    [InlineData(512, "< 1 KB")]
    [InlineData(1023, "< 1 KB")]
    [InlineData(1024, "1 KB")]
    [InlineData(2048, "2 KB")]
    [InlineData(1_048_576, "1.0 MB")]
    [InlineData(3_145_728, "3.0 MB")]
    public void FileSizeFormatted_ReturnsExpectedString(long bytes, string expected)
    {
        var file = new MusicFile { FileSizeBytes = bytes };
        Assert.Equal(expected, file.FileSizeFormatted);
    }
}
