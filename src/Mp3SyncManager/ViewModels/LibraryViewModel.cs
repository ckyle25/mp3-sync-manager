using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mp3SyncManager.Models;
using Mp3SyncManager.Services.Interfaces;

namespace Mp3SyncManager.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly IFileTransferService _fileTransfer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceFolderName))]
    private string _sourceFolderPath = string.Empty;

    public string SourceFolderName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SourceFolderPath)) return string.Empty;
            var trimmed = SourceFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? trimmed : name;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyLibraryMessage))]
    private bool _sourceFolderMissing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiles))]
    private ObservableCollection<MusicFile> _files = [];

    [ObservableProperty]
    private MusicFile? _selectedFile;

    [ObservableProperty]
    private ObservableCollection<MusicFile> _selectedFiles = [];

    public bool HasSelectedFiles => SelectedFiles.Count > 0;

    public LibraryViewModel(IFileTransferService fileTransfer)
    {
        _fileTransfer = fileTransfer;
        // OnSelectedFilesChanged is never called for the field-initializer default,
        // so wire CollectionChanged manually here.
        SubscribeSelectedFiles(_selectedFiles);
    }

    partial void OnSourceFolderPathChanged(string value)
    {
        SourceFolderMissing = !string.IsNullOrWhiteSpace(value) && !Directory.Exists(value);
        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        if (string.IsNullOrWhiteSpace(SourceFolderPath)) return;
        Files = new ObservableCollection<MusicFile>(_fileTransfer.ListFiles(SourceFolderPath, displayRelativePaths: true));
    }
}
