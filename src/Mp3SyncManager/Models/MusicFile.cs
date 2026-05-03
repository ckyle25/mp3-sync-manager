namespace Mp3SyncManager.Models;

public class MusicFile
{
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }

    public string FileSizeFormatted
    {
        get
        {
            if (FileSizeBytes >= 1_048_576)
                return $"{FileSizeBytes / 1_048_576.0:F1} MB";
            if (FileSizeBytes < 1024)
                return "< 1 KB";
            return $"{FileSizeBytes / 1024} KB";
        }
    }
}
