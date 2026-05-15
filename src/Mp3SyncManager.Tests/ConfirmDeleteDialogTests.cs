using Mp3SyncManager.Views;

namespace Mp3SyncManager.Tests;

public class ConfirmDeleteDialogTests
{
    [Fact]
    public void BuildDeleteMessage_IncludesFileName()
    {
        string message = ConfirmDeleteDialog.BuildDeleteMessage("song.mp3");

        Assert.Contains("\"song.mp3\"", message);
    }

    [Fact]
    public void BuildDeleteMessage_MentionsComputer()
    {
        string message = ConfirmDeleteDialog.BuildDeleteMessage("song.mp3");

        Assert.Contains("computer", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildDeleteMessage_MentionsPlayer()
    {
        string message = ConfirmDeleteDialog.BuildDeleteMessage("song.mp3");

        Assert.Contains("player", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildDeleteMessage_EmptyFileName_StillIncludesReassurance()
    {
        string message = ConfirmDeleteDialog.BuildDeleteMessage(string.Empty);

        Assert.Contains("computer", message, StringComparison.OrdinalIgnoreCase);
    }
}
