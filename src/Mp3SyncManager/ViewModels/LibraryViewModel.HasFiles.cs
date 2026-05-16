namespace Mp3SyncManager.ViewModels;

public partial class LibraryViewModel
{
    public bool HasFiles => Artists.Count > 0;

    public bool ShowEmptyLibraryMessage => !SourceFolderMissing && !HasFiles;

    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _selectedFilesCollectionChangedHandler;

    internal void SubscribeSelectedFiles(System.Collections.ObjectModel.ObservableCollection<Mp3SyncManager.Models.MusicFile> collection)
    {
        _selectedFilesCollectionChangedHandler = (_, _) => OnPropertyChanged(nameof(HasSelectedFiles));
        collection.CollectionChanged += _selectedFilesCollectionChangedHandler;
    }

    partial void OnSelectedFilesChanging(System.Collections.ObjectModel.ObservableCollection<Mp3SyncManager.Models.MusicFile>? oldValue,
        System.Collections.ObjectModel.ObservableCollection<Mp3SyncManager.Models.MusicFile> newValue)
    {
        if (oldValue is not null && _selectedFilesCollectionChangedHandler is not null)
            oldValue.CollectionChanged -= _selectedFilesCollectionChangedHandler;
    }

    partial void OnSelectedFilesChanged(System.Collections.ObjectModel.ObservableCollection<Mp3SyncManager.Models.MusicFile> value)
    {
        OnPropertyChanged(nameof(HasSelectedFiles));
        SubscribeSelectedFiles(value);
    }
}
