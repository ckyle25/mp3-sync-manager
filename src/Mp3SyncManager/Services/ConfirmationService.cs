using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Mp3SyncManager.Services.Interfaces;
using Mp3SyncManager.Views;

namespace Mp3SyncManager.Services;

public class ConfirmationService : IConfirmationService
{
    public async Task<bool> ConfirmDeleteAsync(string fileName)
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null) return false;

        var dialog = new ConfirmDeleteDialog(fileName);
        return await dialog.ShowDialog<bool>(owner);
    }
}
