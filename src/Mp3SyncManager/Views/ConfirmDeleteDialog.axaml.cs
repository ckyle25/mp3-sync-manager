using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Mp3SyncManager.Views;

public partial class ConfirmDeleteDialog : Window
{
    internal static string BuildDeleteMessage(string fileName) =>
        $"Remove \"{fileName}\" from the player?\n\nYour music on the computer won't be changed.";

    public ConfirmDeleteDialog() : this(string.Empty) { }

    public ConfirmDeleteDialog(string fileName)
    {
        InitializeComponent();
        MessageText.Text = BuildDeleteMessage(fileName);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void OnConfirmClicked(object? sender, RoutedEventArgs e) => Close(true);
}
