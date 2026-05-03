using Mp3SyncManager.Models;

namespace Mp3SyncManager.Services.Interfaces;

public interface ISettingsService
{
    Task<AppSettings?> LoadAsync();
    Task SaveAsync(AppSettings settings);
    bool IsConfigured(AppSettings? settings);
}
