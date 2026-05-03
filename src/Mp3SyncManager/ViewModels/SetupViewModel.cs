using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;

namespace Mp3SyncManager.ViewModels;

public partial class SetupViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IFileTransferService _fileTransfer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolderSelected))]
    private string _selectedFolderPath = string.Empty;

    public bool HasFolderSelected => !string.IsNullOrWhiteSpace(SelectedFolderPath);

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private int _mp3FileCount;

    [ObservableProperty]
    private bool _confirmVisible;

    public event EventHandler<AppSettings>? SetupCompleted;

    public SetupViewModel(ISettingsService settingsService, IFileTransferService fileTransfer)
    {
        _settingsService = settingsService;
        _fileTransfer = fileTransfer;
    }

    [RelayCommand]
    public void ValidateAndPreview()
    {
        ValidationMessage = null;
        ConfirmVisible = false;

        if (string.IsNullOrWhiteSpace(SelectedFolderPath) || !Directory.Exists(SelectedFolderPath))
        {
            ValidationMessage = "That folder could not be opened. Please try a different one.";
            return;
        }

        var files = _fileTransfer.ListFiles(SelectedFolderPath, displayRelativePaths: true);
        if (files.Count == 0)
        {
            ValidationMessage = "No music files were found in that folder. Please choose a folder that contains music.";
            return;
        }

        Mp3FileCount = files.Count;
        ConfirmVisible = true;
    }

    [RelayCommand]
    public async Task ConfirmAsync()
    {
        if (!ConfirmVisible || string.IsNullOrWhiteSpace(SelectedFolderPath))
            return;

        var settings = new AppSettings
        {
            SourceFolderPath = SelectedFolderPath,
            ConfiguredAt = DateTimeOffset.UtcNow,
        };
        await _settingsService.SaveAsync(settings);
        SetupCompleted?.Invoke(this, settings);
    }

    [RelayCommand]
    public void Reset()
    {
        SelectedFolderPath = string.Empty;
        ValidationMessage = null;
        ConfirmVisible = false;
        Mp3FileCount = 0;
    }
}
