using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Mp3SyncManager.Models;

namespace Mp3SyncManager.ViewModels;

public partial class LibraryAlbumViewModel : ObservableObject
{
    [ObservableProperty]
    private Bitmap? _albumArt;

    public string Name { get; }
    public ObservableCollection<MusicFile> Songs { get; }

    public LibraryAlbumViewModel(string name, IEnumerable<MusicFile> songs)
    {
        Name = name;
        Songs = new ObservableCollection<MusicFile>(songs);
    }
}
