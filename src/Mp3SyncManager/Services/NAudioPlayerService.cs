using Mp3SyncManager.Services.Interfaces;
using NAudio.Wave;

namespace Mp3SyncManager.Services;

public sealed class NAudioPlayerService : IAudioPlayerService
{
    private Mp3FileReader? _reader;
    private WaveOutEvent? _waveOut;
    private System.Timers.Timer? _positionTimer;
    private bool _stoppingIntentionally;

    public TimeSpan Position => _reader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public event EventHandler? PlaybackEnded;
    public event EventHandler? PositionChanged;

    public void Play(string filePath)
    {
        Stop();

        _reader = new Mp3FileReader(filePath);

        _waveOut = new WaveOutEvent();
        _waveOut.Init(_reader);
        _waveOut.PlaybackStopped += OnPlaybackStopped;
        _waveOut.Play();

        _positionTimer = new System.Timers.Timer(250);
        _positionTimer.Elapsed += (_, _) => PositionChanged?.Invoke(this, EventArgs.Empty);
        _positionTimer.Start();
    }

    public void Pause()
    {
        _positionTimer?.Stop();
        _waveOut?.Pause();
    }

    public void Resume()
    {
        _waveOut?.Play();
        _positionTimer?.Start();
    }

    public void Stop()
    {
        _stoppingIntentionally = true;

        _positionTimer?.Stop();
        _positionTimer?.Dispose();
        _positionTimer = null;

        if (_waveOut is not null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }

        _reader?.Dispose();
        _reader = null;

        _stoppingIntentionally = false;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (!_stoppingIntentionally)
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();
}
