using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AudioSwitch.Linux.LinuxCore;

internal enum LinuxMusicKind
{
    Spotify = 0,
    YouTubeMusic = 1
}

internal sealed record LinuxMediaSnapshot(
    bool HasSession,
    string BusName,
    string SourceApp,
    string Title,
    string Artist,
    bool IsPlaying,
    long PositionMicros,
    long LengthMicros);

internal sealed class MprisMediaService : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private Process? _monitorProc;
    private bool _preferYtMusic;
    private string? _activeBus;

    public event Action<LinuxMediaSnapshot>? MediaUpdated;

    public LinuxMediaSnapshot Current { get; private set; } =
        new(false, string.Empty, string.Empty, string.Empty, string.Empty, false, 0, 0);

    public void StartMonitoring()
    {
        RefreshSnapshot();
        _ = Task.Run(() => MonitorSignalsLoop(_cts.Token));
    }

    public void SetPreferredProvider(bool preferYtMusic)
    {
        _preferYtMusic = preferYtMusic;
        RefreshSnapshot();
    }

    public IReadOnlyList<string> ListMprisBusNames()
    {
        var (code, outStr, _) = LinuxCommandRunner.Run("gdbus",
        [
            "call", "--session", "--dest", "org.freedesktop.DBus",
            "--object-path", "/org/freedesktop/DBus",
            "--method", "org.freedesktop.DBus.ListNames"
        ], 2000);

        if (code != 0 || string.IsNullOrWhiteSpace(outStr)) return Array.Empty<string>();

        return Regex.Matches(outStr, @"org\.mpris\.MediaPlayer2\.[^'""\s,]+")
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool HasSessionFor(LinuxMusicKind kind)
        => FindMatchingBus(ListMprisBusNames(), kind) != null;

    public bool TryPlayFor(LinuxMusicKind kind)
    {
        string? bus = FindMatchingBus(ListMprisBusNames(), kind);
        if (string.IsNullOrEmpty(bus)) return false;

        _activeBus = bus;
        LinuxCommandRunner.Run("gdbus",
        [
            "call", "--session", "--dest", bus,
            "--object-path", "/org/mpris/MediaPlayer2",
            "--method", "org.mpris.MediaPlayer2.Player.Play"
        ]);
        RefreshSnapshot();
        return true;
    }

    public void PauseCompetingPlayers(LinuxMusicKind targetKind)
    {
        foreach (string bus in ListMprisBusNames())
        {
            bool isTarget = MatchesKind(bus, targetKind);
            if (isTarget) continue;

            var snap = ReadBusSnapshot(bus);
            if (snap is { IsPlaying: true })
            {
                LinuxCommandRunner.Run("gdbus",
                [
                    "call", "--session", "--dest", bus,
                    "--object-path", "/org/mpris/MediaPlayer2",
                    "--method", "org.mpris.MediaPlayer2.Player.Pause"
                ]);
            }
        }
    }

    public void TogglePlayPause()
    {
        string? bus = ResolveTargetBus();
        if (string.IsNullOrEmpty(bus)) return;

        LinuxCommandRunner.Run("gdbus",
        [
            "call", "--session", "--dest", bus,
            "--object-path", "/org/mpris/MediaPlayer2",
            "--method", "org.mpris.MediaPlayer2.Player.PlayPause"
        ]);
        RefreshSnapshot();
    }

    public void Next()
    {
        string? bus = ResolveTargetBus();
        if (string.IsNullOrEmpty(bus)) return;

        LinuxCommandRunner.Run("gdbus",
        [
            "call", "--session", "--dest", bus,
            "--object-path", "/org/mpris/MediaPlayer2",
            "--method", "org.mpris.MediaPlayer2.Player.Next"
        ]);
    }

    public void Previous()
    {
        string? bus = ResolveTargetBus();
        if (string.IsNullOrEmpty(bus)) return;

        LinuxCommandRunner.Run("gdbus",
        [
            "call", "--session", "--dest", bus,
            "--object-path", "/org/mpris/MediaPlayer2",
            "--method", "org.mpris.MediaPlayer2.Player.Previous"
        ]);
    }

    public LinuxMediaSnapshot RefreshSnapshot()
    {
        var buses = ListMprisBusNames();
        var prefKind = _preferYtMusic ? LinuxMusicKind.YouTubeMusic : LinuxMusicKind.Spotify;

        // Pick playing session first unless preferred player matches actively
        LinuxMediaSnapshot? chosen = null;
        string? prefBus = FindMatchingBus(buses, prefKind);
        if (!string.IsNullOrEmpty(prefBus))
        {
            chosen = ReadBusSnapshot(prefBus);
        }

        if (chosen == null || (!chosen.IsPlaying && buses.Count > 1))
        {
            foreach (string bus in buses)
            {
                var cand = ReadBusSnapshot(bus);
                if (cand is { IsPlaying: true })
                {
                    chosen = cand;
                    break;
                }
                chosen ??= cand;
            }
        }

        chosen ??= new LinuxMediaSnapshot(false, string.Empty, string.Empty, string.Empty, string.Empty, false, 0, 0);
        _activeBus = chosen.HasSession ? chosen.BusName : null;
        Current = chosen;
        MediaUpdated?.Invoke(chosen);
        return chosen;
    }

    private string? ResolveTargetBus()
    {
        var buses = ListMprisBusNames();
        if (!string.IsNullOrEmpty(_activeBus) && buses.Contains(_activeBus, StringComparer.OrdinalIgnoreCase))
        {
            return _activeBus;
        }
        var prefKind = _preferYtMusic ? LinuxMusicKind.YouTubeMusic : LinuxMusicKind.Spotify;
        return FindMatchingBus(buses, prefKind) ?? buses.FirstOrDefault();
    }

    private static string? FindMatchingBus(IReadOnlyList<string> buses, LinuxMusicKind kind)
    {
        return buses.FirstOrDefault(b => MatchesKind(b, kind));
    }

    private static bool MatchesKind(string busName, LinuxMusicKind kind)
    {
        string lower = busName.ToLowerInvariant();
        if (kind == LinuxMusicKind.Spotify)
        {
            return lower.Contains("spotify");
        }
        return lower.Contains("youtube") || lower.Contains("chromium") || lower.Contains("brave") || lower.Contains("chrome") || lower.Contains("edge");
    }

    private static LinuxMediaSnapshot? ReadBusSnapshot(string busName)
    {
        var (code, outStr, _) = LinuxCommandRunner.Run("gdbus",
        [
            "call", "--session", "--dest", busName,
            "--object-path", "/org/mpris/MediaPlayer2",
            "--method", "org.freedesktop.DBus.Properties.GetAll",
            "org.mpris.MediaPlayer2.Player"
        ], 2000);

        if (code != 0 || string.IsNullOrWhiteSpace(outStr)) return null;

        bool isPlaying = outStr.Contains("'PlaybackStatus': <'Playing'>", StringComparison.OrdinalIgnoreCase);
        var titleM = Regex.Match(outStr, @"'xesam:title':\s*<'((?:\\.|[^'])*)'>");
        var artistM = Regex.Match(outStr, @"'xesam:artist':\s*<\['((?:\\.|[^'])*)'");
        var posM = Regex.Match(outStr, @"'Position':\s*<(?:int64\s+)?(\d+)>");
        var lenM = Regex.Match(outStr, @"'mpris:length':\s*<(?:uint64|int64)?\s*(\d+)>");

        string title = titleM.Success ? UnescapeGVariant(titleM.Groups[1].Value) : string.Empty;
        string artist = artistM.Success ? UnescapeGVariant(artistM.Groups[1].Value) : string.Empty;
        long pos = posM.Success && long.TryParse(posM.Groups[1].Value, out long p) ? p : 0;
        long len = lenM.Success && long.TryParse(lenM.Groups[1].Value, out long l) ? l : 0;

        string source = FormatSourceName(busName);
        bool hasTrack = !string.IsNullOrWhiteSpace(title) || isPlaying;
        return new LinuxMediaSnapshot(hasTrack, busName, source, title, artist, isPlaying, pos, len);
    }

    private static string FormatSourceName(string busName)
    {
        string lower = busName.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("youtube") || lower.Contains("pear")) return "YouTube Music";
        if (lower.Contains("brave")) return "Brave";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("chromium")) return "Chromium";
        if (lower.Contains("firefox")) return "Firefox";
        return "Media";
    }

    private static string UnescapeGVariant(string s)
        => s.Replace("\\'", "'").Replace("\\\\", "\\");

    private void MonitorSignalsLoop(CancellationToken token)
    {
        try
        {
            var psi = new ProcessStartInfo("gdbus")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("monitor");
            psi.ArgumentList.Add("--session");
            psi.ArgumentList.Add("--object-path");
            psi.ArgumentList.Add("/org/mpris/MediaPlayer2");

            _monitorProc = Process.Start(psi);
            if (_monitorProc == null) return;

            while (!token.IsCancellationRequested && !_monitorProc.HasExited)
            {
                string? line = _monitorProc.StandardOutput.ReadLine();
                if (line == null) break;
                if (line.Contains("PropertiesChanged", StringComparison.Ordinal))
                {
                    RefreshSnapshot();
                }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            if (_monitorProc is { HasExited: false })
            {
                _monitorProc.Kill(entireProcessTree: true);
            }
        }
        catch { }
        _monitorProc?.Dispose();
        _cts.Dispose();
    }
}
