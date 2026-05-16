using System.Collections.ObjectModel;
using Mp3SyncManager.Models;

namespace Mp3SyncManager.ViewModels;

public class LibraryAlbumViewModel
{
    public string Name { get; }
    public ObservableCollection<MusicFile> Songs { get; }

    public LibraryAlbumViewModel(string name, IEnumerable<MusicFile> songs)
    {
        Name = name;
        Songs = new ObservableCollection<MusicFile>(songs);
    }
}
