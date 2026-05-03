namespace Mp3SyncManager.Models;

public class AppSettings
{
    public string SourceFolderPath { get; set; } = string.Empty;
    public DateTimeOffset ConfiguredAt { get; set; }
}
