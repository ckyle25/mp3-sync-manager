using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Mp3SyncManager.ViewModels;

namespace Mp3SyncManager.Views;

public partial class SetupView : UserControl
{
    public SetupView()
    {
        InitializeComponent();
    }

    private async void OnBrowseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select your music folder"
        });

        if (result.Count > 0)
        {
            ((SetupViewModel)DataContext!).SelectedFolderPath = result[0].Path.LocalPath;
        }
    }
}
