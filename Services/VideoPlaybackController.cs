namespace MuralInstitucional.Services;

public enum VideoPlaybackState { Unavailable, Stopped, Playing, Paused }

/// <summary>Transport semantics separated from codecs and WPF for deterministic verification.</summary>
public sealed class VideoPlaybackController(Action play, Action pause, Action reset, Action close)
{
    public VideoPlaybackState State { get; private set; } = VideoPlaybackState.Unavailable;
    public bool CanPlay => State is VideoPlaybackState.Stopped or VideoPlaybackState.Paused;
    public bool CanPause => State == VideoPlaybackState.Playing;
    public bool CanRestart => State != VideoPlaybackState.Unavailable;
    public event Action? StateChanged;
    private void SetState(VideoPlaybackState state) { State = state; StateChanged?.Invoke(); }
    public void SetAvailable(bool available) => SetState(available ? VideoPlaybackState.Stopped : VideoPlaybackState.Unavailable);
    public void Play()
    {
        if (!CanPlay) return;
        play(); SetState(VideoPlaybackState.Playing);
    }
    public void Pause()
    {
        if (!CanPause) return;
        pause(); SetState(VideoPlaybackState.Paused);
    }
    public void Restart()
    {
        if (!CanRestart) return;
        reset(); play(); SetState(VideoPlaybackState.Playing);
    }
    public void Ended()
    {
        if (State == VideoPlaybackState.Unavailable) return;
        reset(); SetState(VideoPlaybackState.Stopped);
    }
    public void Stop()
    {
        bool available = CanRestart;
        close(); SetState(available ? VideoPlaybackState.Stopped : VideoPlaybackState.Unavailable);
    }
}
