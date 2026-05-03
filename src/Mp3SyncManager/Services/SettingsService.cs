using System.Text.Json;
using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;

namespace Mp3SyncManager.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsDir;
    private readonly string _settingsPath;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Production constructor (used by DI)
    public SettingsService() : this(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mp3SyncManager"))
    { }

    // Testable constructor
    internal SettingsService(string settingsDir)
    {
        _settingsDir = settingsDir;
        _settingsPath = Path.Combine(settingsDir, "settings.json");
    }

    public async Task<AppSettings?> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
            return null;

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(_settingsDir);
        var tempPath = _settingsPath + ".tmp";

        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);

        // Atomic rename prevents a corrupt settings file if the process is killed mid-write
        File.Move(tempPath, _settingsPath, overwrite: true);
    }

    public bool IsConfigured(AppSettings? settings) =>
        settings is { SourceFolderPath.Length: > 0 };
}
