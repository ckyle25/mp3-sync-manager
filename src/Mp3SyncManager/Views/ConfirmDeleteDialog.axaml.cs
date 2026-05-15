using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Mp3SyncManager.Views;

public partial class ConfirmDeleteDialog : Window
{
    public ConfirmDeleteDialog(string fileName)
    {
        InitializeComponent();
        MessageText.Text =
            $"Remove \"{fileName}\" from the player?\n\nYour music on the computer won't be changed.";
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void OnConfirmClicked(object? sender, RoutedEventArgs e) => Close(true);
}
