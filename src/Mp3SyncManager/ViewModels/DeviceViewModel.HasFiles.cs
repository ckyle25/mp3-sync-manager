namespace Mp3SyncManager.ViewModels;

public partial class DeviceViewModel
{
    public bool HasFiles => Files.Count > 0;

    public bool ShowEmptyDeviceMessage => ActiveDevice is not null && !IsLoading && !HasFiles;

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
        OnPropertyChanged(nameof(ShowEmptyDeviceMessage));

        _filesCollectionChangedHandler = (_, _) =>
        {
            OnPropertyChanged(nameof(HasFiles));
            OnPropertyChanged(nameof(ShowEmptyDeviceMessage));
        };
        value.CollectionChanged += _filesCollectionChangedHandler;
    }
}
