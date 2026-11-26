using System.IO;
using System.Windows.Media.Imaging;
using Windows.Media.Control;

namespace AudioSwitch.AudioCore;

internal sealed record MediaSessionSnapshot(
    bool HasSession,
    string Title,
    string Artist,
    string SourceApp,
    bool IsPlaying,
    BitmapImage? Thumbnail,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset LastUpdatedTime,
    double PlaybackRate);

/// <summary>
/// Event-driven Windows SMTC client supporting Spotify, Chrome/YouTube, Edge, VLC, etc.
/// Consumes 0% CPU when idle.
/// </summary>
internal sealed class MediaPlayerService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _disposed;

    public event Action<MediaSessionSnapshot>? MediaUpdated;

    public MediaSessionSnapshot Current { get; private set; } =
        new(false, string.Empty, string.Empty, string.Empty, false, null, TimeSpan.Zero, TimeSpan.Zero, DateTimeOffset.MinValue, 1.0);

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_disposed) return;

            _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
            await BindSessionAsync(_manager.GetCurrentSession());
        }
        catch
        {
            // Ignore if SMTC is unavailable in headless/restricted contexts
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        var session = _currentSession ?? _manager?.GetCurrentSession();
        if (session == null) return;
        try { await session.TryTogglePlayPauseAsync(); } catch { }
    }

    public async Task NextAsync()
    {
        var session = _currentSession ?? _manager?.GetCurrentSession();
        if (session == null) return;
        try { await session.TrySkipNextAsync(); } catch { }
    }

    public async Task PreviousAsync()
    {
        var session = _currentSession ?? _manager?.GetCurrentSession();
        if (session == null) return;
        try { await session.TrySkipPreviousAsync(); } catch { }
    }

    private async void Manager_CurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        if (_disposed) return;
        await BindSessionAsync(sender.GetCurrentSession());
    }

    private async void Session_MediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        if (_disposed || sender != _currentSession) return;
        await RefreshFromSessionAsync(sender);
    }

    private async void Session_PlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        if (_disposed || sender != _currentSession) return;
        await RefreshFromSessionAsync(sender);
    }

    private async void Session_TimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args)
    {
        if (_disposed || sender != _currentSession) return;
        await RefreshFromSessionAsync(sender);
    }

    private async Task BindSessionAsync(GlobalSystemMediaTransportControlsSession? session)
    {
        await _syncLock.WaitAsync();
        try
        {
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
                _currentSession.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
            }

            _currentSession = session;

            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged += Session_MediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged += Session_PlaybackInfoChanged;
                _currentSession.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
            }
        }
        finally
        {
            _syncLock.Release();
        }

        await RefreshFromSessionAsync(session);
    }

    private async Task RefreshFromSessionAsync(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_disposed) return;

        if (session == null)
        {
            Current = new MediaSessionSnapshot(false, string.Empty, string.Empty, string.Empty, false, null, TimeSpan.Zero, TimeSpan.Zero, DateTimeOffset.MinValue, 1.0);
            MediaUpdated?.Invoke(Current);
            return;
        }

        try
        {
            var playback = session.GetPlaybackInfo();
            bool isPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            double rate = playback.PlaybackRate ?? 1.0;
            if (rate <= 0) rate = 1.0;

            var timeline = session.GetTimelineProperties();
            TimeSpan start = timeline?.StartTime ?? TimeSpan.Zero;
            TimeSpan end = timeline?.EndTime ?? TimeSpan.Zero;
            TimeSpan pos = timeline?.Position ?? TimeSpan.Zero;
            TimeSpan duration = end > start ? end - start : TimeSpan.Zero;
            TimeSpan relPos = pos > start ? pos - start : TimeSpan.Zero;
            if (duration > TimeSpan.Zero && relPos > duration) relPos = duration;
            DateTimeOffset lastUpdated = timeline?.LastUpdatedTime ?? DateTimeOffset.UtcNow;
            if (lastUpdated == DateTimeOffset.MinValue) lastUpdated = DateTimeOffset.UtcNow;

            var props = await session.TryGetMediaPropertiesAsync();

            string title = props?.Title ?? string.Empty;
            string artist = props?.Artist ?? string.Empty;
            string source = FormatSourceName(session.SourceAppUserModelId);

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist))
            {
                Current = new MediaSessionSnapshot(false, string.Empty, string.Empty, source, isPlaying, null, TimeSpan.Zero, TimeSpan.Zero, DateTimeOffset.MinValue, 1.0);
                MediaUpdated?.Invoke(Current);
                return;
            }

            BitmapImage? thumb = null;
            if (props?.Thumbnail != null)
            {
                try
                {
                    using var ras = await props.Thumbnail.OpenReadAsync();
                    using var stream = ras.AsStreamForRead();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    ms.Position = 0;

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 96;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    thumb = bmp;
                }
                catch
                {
                    // Ignore malformed thumbnails
                }
            }

            Current = new MediaSessionSnapshot(true, title, artist, source, isPlaying, thumb, relPos, duration, lastUpdated, rate);
            MediaUpdated?.Invoke(Current);
        }
        catch
        {
            // Session closed mid-read
        }
    }

    private static string FormatSourceName(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return "Media";
        string lower = appId.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("msedge")) return "Edge";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("applemusic")) return "Apple Music";
        if (lower.Contains("vlc")) return "VLC";
        if (lower.Contains("foobar")) return "foobar2000";

        int bang = appId.IndexOf('!');
        string clean = bang >= 0 ? appId[(bang + 1)..] : appId;
        return clean.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
        }
        if (_manager != null)
        {
            _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
        }
        _syncLock.Dispose();
    }
}
