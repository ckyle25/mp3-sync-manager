namespace Mp3SyncManager.ViewModels;

// Partial extension of LibraryViewModel that exposes a computed HasFiles property.
// The main LibraryViewModel.cs file raises PropertyChanged for "Files" whenever
// the collection is replaced; we hook that to also notify HasFiles.
public partial class LibraryViewModel
{
    public bool HasFiles => Files.Count > 0;

    public bool ShowEmptyLibraryMessage => !SourceFolderMissing && !HasFiles;

    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _filesCollectionChangedHandler;

    partial void OnFilesChanging(System.Collections.ObjectModel.ObservableCollection<Mp3SyncManager.Models.MusicFile>? oldValue,
        System.Collections.ObjectModel.ObservableCollection<Mp3SyncManager.Models.MusicFile> newValue)
    {
        if (oldValue is not null && _filesCollectionChangedHandler is not null)
            oldValue.CollectionChanged -= _filesCollectionChangedHandler;
    }

    partial void OnFilesChanged(System.Collections.ObjectModel.ObservableCollection<Mp3SyncManager.Models.MusicFile> value)
    {
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(ShowEmptyLibraryMessage));

        _filesCollectionChangedHandler = (_, _) =>
        {
            OnPropertyChanged(nameof(HasFiles));
            OnPropertyChanged(nameof(ShowEmptyLibraryMessage));
        };
        value.CollectionChanged += _filesCollectionChangedHandler;
    }

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
