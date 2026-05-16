namespace Mp3SyncManager.Services.Interfaces;

public enum AudioPlaybackState { Stopped, Playing, Paused }

public interface IAudioPlayerService : IDisposable
{
    TimeSpan Position { get; }
    TimeSpan Duration { get; }

    event EventHandler? PlaybackEnded;   // fires when file ends naturally
    event EventHandler? PositionChanged; // fires ~4x/sec during playback

    void Play(string filePath);
    void Pause();
    void Resume();
    void Stop();
}
