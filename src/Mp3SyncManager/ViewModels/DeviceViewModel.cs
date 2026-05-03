using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;

namespace Mp3SyncManager.ViewModels;

public partial class DeviceViewModel : ViewModelBase
{
    private readonly IFileTransferService _fileTransfer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyDeviceMessage))]
    [NotifyPropertyChangedFor(nameof(DeviceStorageText))]
    private DetectedDevice? _activeDevice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiles))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyDeviceMessage))]
    private ObservableCollection<MusicFile> _files = [];

    [ObservableProperty]
    private MusicFile? _selectedFile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyDeviceMessage))]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    public string? DeviceStorageText
    {
        get
        {
            if (ActiveDevice is null) return null;
            var freeBytes = ActiveDevice.AvailableFreeSpaceBytes;
            var totalBytes = ActiveDevice.TotalSizeBytes;
            if (totalBytes <= 0) return null;

            static string FormatBytes(long bytes)
            {
                if (bytes >= 1_073_741_824)
                    return $"{bytes / 1_073_741_824.0:F1} GB";
                return $"{bytes / 1_048_576.0:F0} MB";
            }

            return $"{FormatBytes(freeBytes)} free of {FormatBytes(totalBytes)}";
        }
    }

    public DeviceViewModel(IFileTransferService fileTransfer)
    {
        _fileTransfer = fileTransfer;
    }

    partial void OnActiveDeviceChanged(DetectedDevice? value)
    {
        Files.Clear();
        StatusMessage = null;
        if (value is null)
        {
            IsLoading = false;
            return;
        }
        RefreshFromDevice(value);
    }

    private void RefreshFromDevice(DetectedDevice snapshot)
    {
        IsLoading = true;
        var result = _fileTransfer.ListFiles(snapshot.RootPath, displayRelativePaths: true);
        if (ActiveDevice?.RootPath != snapshot.RootPath)
        {
            IsLoading = false;
            return;
        }
        Files = new ObservableCollection<MusicFile>(result);
        IsLoading = false;
    }

    [RelayCommand]
    public void Refresh()
    {
        if (ActiveDevice is null) return;
        RefreshFromDevice(ActiveDevice);
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        if (SelectedFile is null || ActiveDevice is null) return;

        try
        {
            await _fileTransfer.DeleteFileFromDeviceAsync(SelectedFile.FullPath, ActiveDevice.RootPath);
            Files.Remove(SelectedFile);
            SelectedFile = null;
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            StatusMessage = ex switch
            {
                FileNotFoundException => "The file was not found on the player.",
                InvalidOperationException => "This file cannot be removed. The player may have been disconnected.",
                _ => "Something went wrong. Please try again.",
            };
        }
    }
}
