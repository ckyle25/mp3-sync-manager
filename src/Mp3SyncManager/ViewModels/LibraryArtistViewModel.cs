using System.Collections.ObjectModel;

namespace Mp3SyncManager.ViewModels;

public class LibraryArtistViewModel
{
    public string Name { get; }
    public ObservableCollection<LibraryAlbumViewModel> Albums { get; }

    public LibraryArtistViewModel(string name, IEnumerable<LibraryAlbumViewModel> albums)
    {
        Name = name;
        Albums = new ObservableCollection<LibraryAlbumViewModel>(albums);
    }
}
