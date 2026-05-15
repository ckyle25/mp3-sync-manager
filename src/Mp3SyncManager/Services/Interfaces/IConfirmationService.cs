namespace Mp3SyncManager.Services.Interfaces;

public interface IConfirmationService
{
    Task<bool> ConfirmDeleteAsync(string fileName);
}
