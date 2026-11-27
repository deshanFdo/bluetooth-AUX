using System.Diagnostics;

namespace AudioSwitch.Linux.LinuxCore;

internal sealed class LinuxMusicLauncherService
{
    private bool _launchInFlight;

    public static bool IsWaylandSession =>
        string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

    public async Task<(bool Playing, string Status)> LaunchAndPlayAsync(
        LinuxMusicKind kind,
        MprisMediaService mpris,
        bool hideToTrayOnX11,
        Action<string>? onProgress = null)
    {
        if (_launchInFlight) return (false, "Starting player...");
        _launchInFlight = true;

        try
        {
            mpris.PauseCompetingPlayers(kind);
            if (mpris.TryPlayFor(kind))
            {
                if (hideToTrayOnX11) HideWindowOnX11(kind);
                return (true, $"Playing {(kind == LinuxMusicKind.Spotify ? "Spotify" : "YouTube Music")}.");
            }

            string label = kind == LinuxMusicKind.Spotify ? "Spotify" : "YouTube Music";
            onProgress?.Invoke($"Launching {label}...");

            if (!StartPlayer(kind))
            {
                return (false, $"{label} not found. Install {label} or Brave/Chrome.");
            }

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 12000)
            {
                await Task.Delay(350);
                if (mpris.TryPlayFor(kind))
                {
                    if (hideToTrayOnX11)
                    {
                        await Task.Delay(250);
                        HideWindowOnX11(kind);
                    }
                    return (true, $"Playing {label}.");
                }
            }

            if (hideToTrayOnX11) HideWindowOnX11(kind);
            return (false, $"{label} opened — pick a track if none was queued.");
        }
        finally
        {
            _launchInFlight = false;
        }
    }

    public static void ToggleWindowOnX11(LinuxMusicKind kind, bool hide)
    {
        if (IsWaylandSession) return;
        if (hide) HideWindowOnX11(kind);
        else ShowWindowOnX11(kind);
    }

    private static void HideWindowOnX11(LinuxMusicKind kind)
    {
        if (IsWaylandSession || !LinuxCommandRunner.CommandExists("wmctrl")) return;

        string pattern = kind == LinuxMusicKind.Spotify ? "Spotify" : "YouTube Music";
        var (code, outStr, _) = LinuxCommandRunner.Run("wmctrl", ["-l"]);
        if (code != 0) return;

        foreach (string line in outStr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains(pattern, StringComparison.OrdinalIgnoreCase)) continue;
            string wid = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrEmpty(wid)) continue;

            // Keep Spotify/YT Music out of Ubuntu Dock/taskbar while MPRIS & tray controls remain active
            LinuxCommandRunner.Run("wmctrl", ["-i", "-r", wid, "-b", "add,skip_taskbar,skip_pager"]);
            if (LinuxCommandRunner.CommandExists("xdotool"))
            {
                LinuxCommandRunner.Run("xdotool", ["windowminimize", wid]);
            }
        }
    }

    private static void ShowWindowOnX11(LinuxMusicKind kind)
    {
        if (IsWaylandSession || !LinuxCommandRunner.CommandExists("wmctrl")) return;
        string pattern = kind == LinuxMusicKind.Spotify ? "Spotify" : "YouTube Music";
        LinuxCommandRunner.Run("wmctrl", ["-a", pattern]);
    }

    private static bool StartPlayer(LinuxMusicKind kind)
    {
        if (kind == LinuxMusicKind.Spotify)
        {
            if (LinuxCommandRunner.CommandExists("spotify"))
            {
                LinuxCommandRunner.StartDetached("spotify", []);
                return true;
            }
            if (LinuxCommandRunner.CommandExists("flatpak"))
            {
                var (c, outS, _) = LinuxCommandRunner.Run("flatpak", ["list", "--app"]);
                if (c == 0 && outS.Contains("com.spotify.Client", StringComparison.OrdinalIgnoreCase))
                {
                    LinuxCommandRunner.StartDetached("flatpak", ["run", "com.spotify.Client"]);
                    return true;
                }
            }
            if (LinuxCommandRunner.CommandExists("snap"))
            {
                LinuxCommandRunner.StartDetached("snap", ["run", "spotify"]);
                return true;
            }
            LinuxCommandRunner.StartDetached("xdg-open", ["spotify:"]);
            return true;
        }

        // YouTube Music: dedicated desktop wrappers -> Brave/Chrome/Edge --app -> default browser
        foreach (string bin in new[] { "youtube-music", "pear-desktop", "youtube-music-desktop-app" })
        {
            if (LinuxCommandRunner.CommandExists(bin))
            {
                LinuxCommandRunner.StartDetached(bin, []);
                return true;
            }
        }

        string cfg = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var browsers = new (string Cmd, string ProfileDir)[]
        {
            ("brave-browser", Path.Combine(cfg, "BraveSoftware", "Brave-Browser")),
            ("google-chrome", Path.Combine(cfg, "google-chrome")),
            ("chromium", Path.Combine(cfg, "chromium")),
            ("chromium-browser", Path.Combine(cfg, "chromium")),
            ("microsoft-edge", Path.Combine(cfg, "microsoft-edge"))
        };

        string? chosenBrowser = browsers
            .Where(b => LinuxCommandRunner.CommandExists(b.Cmd))
            .OrderByDescending(b => HasYtMusicProfileData(b.ProfileDir))
            .Select(b => b.Cmd)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(chosenBrowser))
        {
            LinuxCommandRunner.StartDetached(chosenBrowser, ["--app=https://music.youtube.com"]);
            return true;
        }

        LinuxCommandRunner.StartDetached("xdg-open", ["https://music.youtube.com"]);
        return true;
    }

    private static bool HasYtMusicProfileData(string profileRoot)
    {
        if (!Directory.Exists(profileRoot)) return false;
        try
        {
            return Directory.EnumerateDirectories(profileRoot, "*music.youtube.com*", SearchOption.AllDirectories).Any();
        }
        catch
        {
            return false;
        }
    }
}
