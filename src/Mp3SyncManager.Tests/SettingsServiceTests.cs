using Mp3SyncManager.Models;
using Mp3SyncManager.Services;

namespace Mp3SyncManager.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _settingsDir;
    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        _settingsDir = Path.Combine(Path.GetTempPath(), "Mp3SyncManagerTests", Guid.NewGuid().ToString());
        // Do not pre-create the directory — tests that need it will create it themselves
        _sut = new SettingsService(_settingsDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_settingsDir))
            Directory.Delete(_settingsDir, recursive: true);
    }

    [Fact]
    public async Task LoadAsync_WhenNoFileExists_ReturnsNull()
    {
        var result = await _sut.LoadAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsCorrupt_ReturnsNull()
    {
        Directory.CreateDirectory(_settingsDir);
        var settingsPath = Path.Combine(_settingsDir, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "this is not valid json {{{{");

        var result = await _sut.LoadAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsValid_ReturnsSettings()
    {
        var saved = new AppSettings
        {
            SourceFolderPath = @"C:\Music\Library",
            ConfiguredAt = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero),
        };

        await _sut.SaveAsync(saved);
        var loaded = await _sut.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(saved.SourceFolderPath, loaded.SourceFolderPath);
        Assert.Equal(saved.ConfiguredAt, loaded.ConfiguredAt);
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfMissing()
    {
        // Deliberately use a nested path that does not yet exist
        var nestedDir = Path.Combine(_settingsDir, "deep", "nested");
        var nestedService = new SettingsService(nestedDir);

        await nestedService.SaveAsync(new AppSettings { SourceFolderPath = @"C:\Music" });

        Assert.True(Directory.Exists(nestedDir));
        Assert.True(File.Exists(Path.Combine(nestedDir, "settings.json")));
    }

    [Fact]
    public async Task SaveAsync_IsAtomic_LeavesNoTempFile()
    {
        await _sut.SaveAsync(new AppSettings { SourceFolderPath = @"C:\Music" });

        var tmpPath = Path.Combine(_settingsDir, "settings.json.tmp");
        Assert.False(File.Exists(tmpPath));
    }

    [Fact]
    public void IsConfigured_NullSettings_ReturnsFalse()
    {
        Assert.False(_sut.IsConfigured(null));
    }

    [Fact]
    public void IsConfigured_EmptyPath_ReturnsFalse()
    {
        Assert.False(_sut.IsConfigured(new AppSettings { SourceFolderPath = "" }));
    }

    [Fact]
    public void IsConfigured_WithPath_ReturnsTrue()
    {
        // IsConfigured only checks that the string is non-empty — it does not call Directory.Exists
        Assert.True(_sut.IsConfigured(new AppSettings { SourceFolderPath = @"C:\some\path" }));
    }
}
