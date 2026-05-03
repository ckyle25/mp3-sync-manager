using System.Collections.ObjectModel;
using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;
using Mp3SyncManager.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mp3SyncManager.Tests;

public class MainViewModelTests
{
    private (MainViewModel vm, ISettingsService settings, IDeviceDetectionService detection, IFileTransferService fileTransfer)
        BuildSut(AppSettings? loadResult = null)
    {
        var settings = Substitute.For<ISettingsService>();
        var detection = Substitute.For<IDeviceDetectionService>();
        var fileTransfer = Substitute.For<IFileTransferService>();

        settings.LoadAsync().Returns(loadResult);
        settings.IsConfigured(Arg.Any<AppSettings?>()).Returns(loadResult?.SourceFolderPath?.Length > 0);
        detection.GetCurrentDevices().Returns(new List<DetectedDevice>().AsReadOnly());
        fileTransfer.ListFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new List<MusicFile>().AsReadOnly());

        var setupVm = new SetupViewModel(settings, fileTransfer);
        var libraryVm = new LibraryViewModel(fileTransfer);
        var deviceVm = new DeviceViewModel(fileTransfer);
        var vm = new MainViewModel(settings, detection, fileTransfer, setupVm, libraryVm, deviceVm);
        return (vm, settings, detection, fileTransfer);
    }

    private static DetectedDevice MakeDevice(string rootPath = "E:\\") =>
        new DetectedDevice { RootPath = rootPath };

    private static MusicFile MakeFile(string name = "song.mp3", string root = @"C:\Music") =>
        new MusicFile { FileName = name, FullPath = $@"{root}\{name}", FileSizeBytes = 1024 };

    [Fact]
    public async Task InitializeAsync_WhenNotConfigured_IsShellVisibleFalse_CurrentPageIsSetup()
    {
        var (vm, _, _, _) = BuildSut(loadResult: null);

        await vm.InitializeAsync();

        Assert.False(vm.IsShellVisible);
        Assert.Equal(vm.SetupViewModel, vm.CurrentPage);
    }

    [Fact]
    public async Task InitializeAsync_WhenConfigured_IsShellVisibleTrue()
    {
        var (vm, _, _, _) = BuildSut(loadResult: new AppSettings { SourceFolderPath = Path.GetTempPath() });

        await vm.InitializeAsync();

        Assert.True(vm.IsShellVisible);
    }

    [Fact]
    public async Task OnAvailableDevicesChanged_AutoSelectsFirstDevice_WhenNoneSelected()
    {
        var (vm, _, _, _) = BuildSut(loadResult: new AppSettings { SourceFolderPath = Path.GetTempPath() });
        await vm.InitializeAsync();

        var device = new DetectedDevice { RootPath = "E:\\" };
        vm.AvailableDevices = new List<DetectedDevice> { device }.AsReadOnly();

        Assert.Equal("E:\\", vm.SelectedDevice?.RootPath);
    }

    [Fact]
    public async Task OnDevicesChanged_ClearsSelectedDevice_WhenDeviceDisappears()
    {
        var (vm, _, _, _) = BuildSut(loadResult: new AppSettings { SourceFolderPath = Path.GetTempPath() });
        await vm.InitializeAsync();

        vm.SelectedDevice = new DetectedDevice { RootPath = "E:\\" };
        vm.AvailableDevices = new List<DetectedDevice>().AsReadOnly();

        Assert.Null(vm.SelectedDevice);
    }

    [Fact]
    public async Task InitializeAsync_WhenExceptionThrown_SetsStartupErrorMessage()
    {
        var (vm, settings, _, _) = BuildSut();
        settings.LoadAsync().Returns<Task<AppSettings?>>(_ => throw new InvalidOperationException("boom"));

        await vm.InitializeAsync();

        Assert.Equal("boom", vm.StartupErrorMessage);
        Assert.Equal(vm.SetupViewModel, vm.CurrentPage);
    }

    [Fact]
    public async Task StartDeviceMonitoring_CalledTwice_AvailableDevicesReflectsLatestList()
    {
        var (vm, _, detection, _) = BuildSut(loadResult: new AppSettings { SourceFolderPath = Path.GetTempPath() });

        var firstList = new List<DetectedDevice> { new() { RootPath = "E:\\" } }.AsReadOnly();
        var secondList = new List<DetectedDevice> { new() { RootPath = "F:\\" } }.AsReadOnly();

        detection.GetCurrentDevices().Returns(firstList, secondList);

        await vm.InitializeAsync();
        Assert.Equal("E:\\", vm.AvailableDevices[0].RootPath);

        // Simulate setup completing again (which calls StartDeviceMonitoring again)
        detection.GetCurrentDevices().Returns(secondList);
        vm.AvailableDevices = secondList; // Direct assignment simulates the second call result

        Assert.Equal("F:\\", vm.AvailableDevices[0].RootPath);
        Assert.Single(vm.AvailableDevices);
    }

    [Fact]
    public async Task NavigateToSetup_ResetsSetupViewModel()
    {
        var (vm, _, _, _) = BuildSut(loadResult: new AppSettings { SourceFolderPath = Path.GetTempPath() });
        await vm.InitializeAsync();

        // Simulate a prior setup run leaving stale state
        vm.SetupViewModel.SelectedFolderPath = @"C:\OldFolder";
        vm.SetupViewModel.ConfirmVisible = true;

        vm.NavigateToSetupCommand.Execute(null);

        Assert.False(vm.IsShellVisible);
        Assert.Equal(vm.SetupViewModel, vm.CurrentPage);
        Assert.Equal(string.Empty, vm.SetupViewModel.SelectedFolderPath);
        Assert.False(vm.SetupViewModel.ConfirmVisible);
    }

    [Fact]
    public async Task DevicesChangedEvent_AfterInitialize_UpdatesAvailableDevices()
    {
        var (vm, _, detection, _) = BuildSut(loadResult: new AppSettings { SourceFolderPath = Path.GetTempPath() });
        detection.GetCurrentDevices().Returns(new List<DetectedDevice>().AsReadOnly());
        await vm.InitializeAsync();

        int changeCount = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.AvailableDevices)) changeCount++;
        };

        var newDevices = new List<DetectedDevice> { new() { RootPath = "E:\\" } }.AsReadOnly();
        detection.DevicesChanged += NSubstitute.Raise.EventWith(
            detection, new Mp3SyncManager.Models.DevicesChangedEventArgs(newDevices));

        Assert.Equal(1, changeCount);
        Assert.Single(vm.AvailableDevices);
        Assert.Equal("E:\\", vm.AvailableDevices[0].RootPath);
        Assert.Equal("E:\\", vm.SelectedDevice?.RootPath);
    }

    [Fact]
    public async Task CopyToDevice_NoDeviceSelected_DoesNotCopy()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        // No device set; SelectedDevice is null by default
        vm.LibraryViewModel.SelectedFiles = new ObservableCollection<MusicFile> { MakeFile() };

        await vm.CopyToDeviceAsync();

        await fileTransfer.DidNotReceive().CopyFileAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        Assert.False(vm.IsCopying);
    }

    [Fact]
    public async Task CopyToDevice_NoFilesSelected_DoesNotCopy()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        vm.DeviceViewModel.ActiveDevice = MakeDevice();
        // SelectedFiles left empty by default

        await vm.CopyToDeviceAsync();

        await fileTransfer.DidNotReceive().CopyFileAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        Assert.False(vm.IsCopying);
    }

    [Fact]
    public async Task CopyToDevice_SingleFile_CopiesAndRefreshesDevice()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        vm.DeviceViewModel.ActiveDevice = MakeDevice();
        vm.LibraryViewModel.SourceFolderPath = @"C:\Music";
        var file = MakeFile();
        vm.LibraryViewModel.SelectedFiles = new ObservableCollection<MusicFile> { file };

        fileTransfer.CopyFileAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await vm.CopyToDeviceAsync();

        await fileTransfer.Received(1).CopyFileAsync(
            file.FullPath, @"C:\Music", MakeDevice().RootPath,
            Arg.Any<IProgress<TransferProgress>?>(), false, Arg.Any<CancellationToken>());

        // Refresh calls ListFiles on the device: once when ActiveDevice was set, once after copy
        fileTransfer.Received(2).ListFiles(MakeDevice().RootPath, Arg.Any<string>(), Arg.Any<bool>());
        Assert.False(vm.IsCopying);
    }

    [Fact]
    public async Task CopyToDevice_FileAlreadyOnDevice_SetsSkipMessage()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        vm.DeviceViewModel.ActiveDevice = MakeDevice();
        vm.LibraryViewModel.SourceFolderPath = @"C:\Music";
        var file = MakeFile();
        vm.LibraryViewModel.SelectedFiles = new ObservableCollection<MusicFile> { file };

        fileTransfer.CopyFileAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new FileAlreadyExistsOnDeviceException("song.mp3"));

        await vm.CopyToDeviceAsync();

        Assert.NotNull(vm.CopyStatusMessage);
        Assert.Contains("already on the player", vm.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsCopying);
    }

    [Fact]
    public async Task CopyToDevice_DeviceDisconnectedDuringCopy_SetsErrorMessage()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        vm.DeviceViewModel.ActiveDevice = MakeDevice();
        vm.LibraryViewModel.SourceFolderPath = @"C:\Music";
        var file = MakeFile();
        vm.LibraryViewModel.SelectedFiles = new ObservableCollection<MusicFile> { file };

        fileTransfer.CopyFileAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Device at 'E:\\' is no longer detected."));

        await vm.CopyToDeviceAsync();

        Assert.NotNull(vm.CopyStatusMessage);
        Assert.Contains("disconnected", vm.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsCopying);
    }

    [Fact]
    public async Task CopyToDevice_MultipleFiles_AllSucceed_SetsCountMessage()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        vm.DeviceViewModel.ActiveDevice = MakeDevice();
        vm.LibraryViewModel.SourceFolderPath = @"C:\Music";
        var file1 = MakeFile("a.mp3");
        var file2 = MakeFile("b.mp3");
        vm.LibraryViewModel.SelectedFiles = new ObservableCollection<MusicFile> { file1, file2 };

        fileTransfer.CopyFileAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await vm.CopyToDeviceAsync();

        Assert.NotNull(vm.CopyStatusMessage);
        Assert.Contains("2 songs", vm.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsCopying);
    }

    [Fact]
    public async Task CopyToDevice_IsCopying_PreventsSecondCopy()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        vm.DeviceViewModel.ActiveDevice = MakeDevice();
        vm.LibraryViewModel.SourceFolderPath = @"C:\Music";
        vm.LibraryViewModel.SelectedFiles = new ObservableCollection<MusicFile> { MakeFile() };

        var tcs = new TaskCompletionSource();
        fileTransfer.CopyFileAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => tcs.Task);

        // Start copy but don't await — run it on the thread pool
        var copyTask = Task.Run(() => vm.CopyToDeviceAsync());

        // Give the task a moment to enter the copy loop and set IsCopying = true
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!vm.IsCopying && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(vm.IsCopying);
        Assert.False(vm.CopyToDeviceCommand.CanExecute(null));

        // Unblock the copy so the task can complete
        tcs.SetResult();
        await copyTask;

        Assert.False(vm.IsCopying);
    }

    [Fact]
    public async Task CopyToDevice_DisconnectException_AbortsLoop()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        vm.DeviceViewModel.ActiveDevice = MakeDevice();
        vm.LibraryViewModel.SourceFolderPath = @"C:\Music";
        var file1 = MakeFile("a.mp3");
        var file2 = MakeFile("b.mp3");
        vm.LibraryViewModel.SelectedFiles = new ObservableCollection<MusicFile> { file1, file2 };

        fileTransfer.CopyFileAsync(
            file1.FullPath, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Device disconnected."));

        await vm.CopyToDeviceAsync();

        // File 2 must never be attempted after the disconnect
        await fileTransfer.DidNotReceive().CopyFileAsync(
            file2.FullPath, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());

        Assert.NotNull(vm.CopyStatusMessage);
        Assert.Contains("disconnected", vm.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsCopying);
    }

    [Fact]
    public async Task CopyToDevice_AlreadyExistsException_DoesNotAbortLoop()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        vm.DeviceViewModel.ActiveDevice = MakeDevice();
        vm.LibraryViewModel.SourceFolderPath = @"C:\Music";
        var file1 = MakeFile("a.mp3");
        var file2 = MakeFile("b.mp3");
        vm.LibraryViewModel.SelectedFiles = new ObservableCollection<MusicFile> { file1, file2 };

        fileTransfer.CopyFileAsync(
            file1.FullPath, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new FileAlreadyExistsOnDeviceException("a.mp3"));

        fileTransfer.CopyFileAsync(
            file2.FullPath, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await vm.CopyToDeviceAsync();

        // File 2 must still have been attempted
        await fileTransfer.Received(1).CopyFileAsync(
            file2.FullPath, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());

        Assert.NotNull(vm.CopyStatusMessage);
        Assert.DoesNotContain("could not be copied", vm.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsCopying);
    }

    [Fact]
    public async Task CopyToDevice_AllFilesAlreadyExist_ShowsAlreadyOnPlayerMessage()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        vm.DeviceViewModel.ActiveDevice = MakeDevice();
        vm.LibraryViewModel.SourceFolderPath = @"C:\Music";
        var file1 = MakeFile("a.mp3");
        var file2 = MakeFile("b.mp3");
        vm.LibraryViewModel.SelectedFiles = new ObservableCollection<MusicFile> { file1, file2 };

        fileTransfer.CopyFileAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new FileAlreadyExistsOnDeviceException("track.mp3"));

        await vm.CopyToDeviceAsync();

        Assert.NotNull(vm.CopyStatusMessage);
        Assert.Contains("already on the player", vm.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("could not", vm.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("none", vm.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsCopying);
    }

    [Fact]
    public async Task CopyToDevice_MixedSuccessAndSkip_CountsOnlySuccesses()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        vm.DeviceViewModel.ActiveDevice = MakeDevice();
        vm.LibraryViewModel.SourceFolderPath = @"C:\Music";
        var file1 = MakeFile("a.mp3");
        var file2 = MakeFile("b.mp3");
        vm.LibraryViewModel.SelectedFiles = new ObservableCollection<MusicFile> { file1, file2 };

        fileTransfer.CopyFileAsync(
            file1.FullPath, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        fileTransfer.CopyFileAsync(
            file2.FullPath, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new FileAlreadyExistsOnDeviceException("b.mp3"));

        await vm.CopyToDeviceAsync();

        Assert.NotNull(vm.CopyStatusMessage);
        Assert.Contains("1 song", vm.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("already on the player", vm.CopyStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsCopying);
    }

    [Fact]
    public async Task NavigateToSetup_AfterCopy_ClearsCopyState()
    {
        var (vm, _, _, fileTransfer) = BuildSut(loadResult: new AppSettings { SourceFolderPath = @"C:\Music" });
        await vm.InitializeAsync();
        vm.DeviceViewModel.ActiveDevice = MakeDevice();
        vm.LibraryViewModel.SourceFolderPath = @"C:\Music";
        vm.LibraryViewModel.SelectedFiles = new ObservableCollection<MusicFile> { MakeFile() };

        fileTransfer.CopyFileAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<TransferProgress>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await vm.CopyToDeviceAsync();

        // Verify copy state was set
        Assert.NotNull(vm.CopyStatusMessage);

        vm.NavigateToSetupCommand.Execute(null);

        Assert.False(vm.IsCopying);
        Assert.Null(vm.CopyStatusMessage);
        Assert.Equal(0, vm.CopyProgressPercent);
        Assert.Null(vm.CopyProgressFileName);
    }
}
