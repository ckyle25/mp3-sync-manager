using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;

namespace Mp3SyncManager.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IDeviceDetectionService _deviceDetection;
    private readonly IFileTransferService _fileTransfer;

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private AppSettings? _settings;

    [ObservableProperty]
    private IReadOnlyList<DetectedDevice> _availableDevices = [];

    [ObservableProperty]
    private DetectedDevice? _selectedDevice;

    [ObservableProperty]
    private bool _sourceWarningVisible;

    [ObservableProperty]
    private string? _startupErrorMessage;

    [ObservableProperty]
    private bool _isShellVisible;

    [ObservableProperty]
    private bool _isDeviceSectionVisible;

    [ObservableProperty]
    private bool _isCopying;

    [ObservableProperty]
    private string? _copyStatusMessage;

    [ObservableProperty]
    private int _copyProgressPercent;

    [ObservableProperty]
    private string? _copyProgressFileName;

    public SetupViewModel SetupViewModel { get; }
    public LibraryViewModel LibraryViewModel { get; }
    public DeviceViewModel DeviceViewModel { get; }

    public MainViewModel(
        ISettingsService settingsService,
        IDeviceDetectionService deviceDetection,
        IFileTransferService fileTransfer,
        SetupViewModel setupViewModel,
        LibraryViewModel libraryViewModel,
        DeviceViewModel deviceViewModel)
    {
        _settingsService = settingsService;
        _deviceDetection = deviceDetection;
        _fileTransfer = fileTransfer;
        SetupViewModel = setupViewModel;
        LibraryViewModel = libraryViewModel;
        DeviceViewModel = deviceViewModel;

        setupViewModel.SetupCompleted += OnSetupCompleted;

        LibraryViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryViewModel.HasSelectedFiles))
                CopyToDeviceCommand.NotifyCanExecuteChanged();
            if (e.PropertyName == nameof(LibraryViewModel.HasSelectedAlbum))
                CopyAlbumToDeviceCommand.NotifyCanExecuteChanged();
        };

        DeviceViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceViewModel.ActiveDevice))
            {
                CopyToDeviceCommand.NotifyCanExecuteChanged();
                CopyAlbumToDeviceCommand.NotifyCanExecuteChanged();
            }
        };
    }

    partial void OnIsCopyingChanged(bool value)
    {
        CopyToDeviceCommand.NotifyCanExecuteChanged();
        CopyAlbumToDeviceCommand.NotifyCanExecuteChanged();
        NavigateToSetupCommand.NotifyCanExecuteChanged();
    }

    private bool CanCopyToDevice() =>
        !IsCopying &&
        DeviceViewModel.ActiveDevice is not null &&
        LibraryViewModel.HasSelectedFiles;

    [RelayCommand(CanExecute = nameof(CanCopyToDevice))]
    public async Task CopyToDeviceAsync()
    {
        if (DeviceViewModel.ActiveDevice is null ||
            LibraryViewModel.SelectedFiles.Count == 0 ||
            IsCopying)
            return;

        await ExecuteCopyAsync(LibraryViewModel.SelectedFiles.ToList());
    }

    private bool CanCopyAlbumToDevice() =>
        !IsCopying &&
        DeviceViewModel.ActiveDevice is not null &&
        LibraryViewModel.HasSelectedAlbum;

    [RelayCommand(CanExecute = nameof(CanCopyAlbumToDevice))]
    public async Task CopyAlbumToDeviceAsync()
    {
        if (DeviceViewModel.ActiveDevice is null ||
            LibraryViewModel.SelectedAlbum is null ||
            IsCopying)
            return;

        await ExecuteCopyAsync(LibraryViewModel.SelectedAlbum.Songs.ToList());
    }

    private async Task ExecuteCopyAsync(List<MusicFile> files)
    {
        if (DeviceViewModel.ActiveDevice is null || files.Count == 0)
            return;

        IsCopying = true;
        CopyStatusMessage = null;
        CopyProgressPercent = 0;
        CopyProgressFileName = null;

        var device = DeviceViewModel.ActiveDevice;
        var sourceRoot = LibraryViewModel.SourceFolderPath;
        int total = files.Count;
        int completed = 0;
        var skipped = new List<string>();
        var failed = new List<string>();
        bool disconnected = false;

        try
        {
            foreach (var file in files)
            {
                CopyProgressFileName = file.FileName;
                var fileProgress = new Progress<TransferProgress>(p =>
                {
                    double filePercent = total == 0 ? 100 : p.TotalBytes == 0 ? 100 : (double)p.BytesTransferred / p.TotalBytes * 100;
                    CopyProgressPercent = (int)((completed * 100 + filePercent) / total);
                });

                try
                {
                    await _fileTransfer.CopyFileAsync(
                        file.FullPath,
                        sourceRoot,
                        device.RootPath,
                        fileProgress,
                        overwriteExisting: false,
                        CancellationToken.None);
                    completed++;
                }
                catch (FileAlreadyExistsOnDeviceException)
                {
                    skipped.Add($"\"{file.FileName}\" is already on the player.");
                }
                catch (DeviceNotAvailableException)
                {
                    failed.Add($"\"{file.FileName}\" could not be added. The player may have been disconnected.");
                    disconnected = true;
                    break;
                }
                catch (Exception)
                {
                    failed.Add($"\"{file.FileName}\" could not be added.");
                }
            }

            if (!disconnected)
                CopyProgressPercent = 100;
            CopyProgressFileName = null;

            if (!disconnected)
                DeviceViewModel.Refresh();

            if (failed.Count == 0 && skipped.Count == 0)
            {
                CopyStatusMessage = completed == 1
                    ? "Added to your player!"
                    : $"Added {completed} song{(completed == 1 ? "" : "s")} to your player!";
            }
            else if (failed.Count == 0 && skipped.Count > 0 && completed > 0)
            {
                CopyStatusMessage = $"{completed} song{(completed == 1 ? "" : "s")} added. " +
                    $"{skipped.Count} {(skipped.Count == 1 ? "song was" : "songs were")} already on the player.";
            }
            else if (failed.Count == 0 && skipped.Count > 0 && completed == 0)
            {
                CopyStatusMessage = skipped.Count == 1
                    ? "That song is already on the player."
                    : "All of these songs are already on the player.";
            }
            else if (completed > 0)
            {
                CopyStatusMessage = $"{completed} song{(completed == 1 ? "" : "s")} added. Some could not be copied.";
            }
            else
            {
                CopyStatusMessage = disconnected
                    ? "The player was disconnected. Please reconnect and try again."
                    : "None of the songs could be added. Please try again.";
            }

            if (disconnected)
                SelectedDevice = null;
        }
        finally
        {
            IsCopying = false;
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            Settings = await _settingsService.LoadAsync();

            if (!_settingsService.IsConfigured(Settings))
            {
                CurrentPage = SetupViewModel;
                return;
            }

            SourceWarningVisible = !Directory.Exists(Settings!.SourceFolderPath);
            LibraryViewModel.SourceFolderPath = Settings.SourceFolderPath;

            StartDeviceMonitoring();
            IsShellVisible = true;
        }
        catch (Exception ex)
        {
            StartupErrorMessage = ex.Message;
            CurrentPage = SetupViewModel;
        }
    }

    private void StartDeviceMonitoring()
    {
        _deviceDetection.DevicesChanged -= OnDevicesChanged;
        _deviceDetection.DevicesChanged += OnDevicesChanged;
        _deviceDetection.StartMonitoring(TimeSpan.FromSeconds(2));
        AvailableDevices = _deviceDetection.GetCurrentDevices();
    }

    private void OnDevicesChanged(object? sender, DevicesChangedEventArgs e)
    {
        AvailableDevices = e.Devices;
    }

    private void OnSetupCompleted(object? sender, AppSettings settings)
    {
        Settings = settings;
        SourceWarningVisible = false;
        LibraryViewModel.SourceFolderPath = settings.SourceFolderPath;
        StartDeviceMonitoring();
        IsShellVisible = true;
    }

    private bool CanNavigateToSetup() => !IsCopying;

    [RelayCommand(CanExecute = nameof(CanNavigateToSetup))]
    public void NavigateToSetup()
    {
        _deviceDetection.StopMonitoring();
        _deviceDetection.DevicesChanged -= OnDevicesChanged;
        AvailableDevices = [];
        SelectedDevice = null;
        IsShellVisible = false;
        IsCopying = false;
        CopyStatusMessage = null;
        CopyProgressPercent = 0;
        CopyProgressFileName = null;
        SetupViewModel.Reset();
        CurrentPage = SetupViewModel;
    }

    partial void OnSelectedDeviceChanged(DetectedDevice? value)
    {
        DeviceViewModel.ActiveDevice = value;
    }

    partial void OnAvailableDevicesChanged(IReadOnlyList<DetectedDevice> value)
    {
        IsDeviceSectionVisible = value.Count > 0;
        if (SelectedDevice is null && value.Count > 0)
            SelectedDevice = value[0];
        if (SelectedDevice is not null && !value.Any(d => d.RootPath == SelectedDevice.RootPath))
            SelectedDevice = null;
    }
}
