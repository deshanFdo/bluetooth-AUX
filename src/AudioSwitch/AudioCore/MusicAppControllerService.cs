using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using Microsoft.Win32;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace AudioSwitch.AudioCore;

internal enum MusicAppKind
{
    Spotify = 0,
    YouTubeMusic = 1
}

/// <summary>
/// Unified event-driven controller that launches Spotify or YouTube Music on demand,
/// triggers SMTC / media-key playback, and tucks app windows off the taskbar into
/// dedicated Windows Hidden Tray Icons (^) with 0% idle CPU.
/// </summary>
internal sealed class MusicAppControllerService : IDisposable
{
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private const uint WmClose = 0x0010;
    private const uint WmAppCommand = 0x0319;
    private const int AppCommandMediaPlayPause = 14;
    private const int AppCommandMediaPlay = 46;
    private const ushort VkMediaPlayPause = 0xB3;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;

    private static readonly string[] ChromiumBrowserProcesses =
    [
        "brave", "chrome", "msedge", "chrome_proxy", "msedge_proxy"
    ];

    private static readonly string[] DedicatedYtMusicProcesses =
    [
        "youtube music", "ytmdesktop", "pear-desktop", "youtube music desktop app"
    ];

    private static readonly string[] MultiTabBrowserTitleSuffixes =
    [
        " - Brave", " - Google Chrome", " - Microsoft Edge", " - Mozilla Firefox", " - Opera"
    ];

    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarListComObject { }

    [ComImport]
    [Guid("56FDF342-FD6D-11d0-958A-006097C9A090")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    private sealed class AppSlot
    {
        public required MusicAppKind Kind { get; init; }
        public required string DisplayName { get; init; }
        public HashSet<IntPtr> KnownHwnds { get; } = new();
        public Forms.NotifyIcon? TrayIcon { get; set; }
        public Drawing.Icon? IconHandle { get; set; }
        public IntPtr IconGdiHandle { get; set; }
        public bool UserRequestedVisible { get; set; }
    }

    private readonly Dispatcher _dispatcher;
    private readonly Func<Task> _onTogglePlayPause;
    private readonly Func<Task> _onNextTrack;
    private readonly Func<Task> _onPreviousTrack;
    private readonly ITaskbarList? _taskbarList;
    private readonly Dictionary<MusicAppKind, AppSlot> _slots;

    private bool _launchInFlight;
    private bool _disposed;

    public MusicAppControllerService(
        Dispatcher dispatcher,
        Func<Task> onTogglePlayPause,
        Func<Task> onNextTrack,
        Func<Task> onPreviousTrack)
    {
        _dispatcher = dispatcher;
        _onTogglePlayPause = onTogglePlayPause;
        _onNextTrack = onNextTrack;
        _onPreviousTrack = onPreviousTrack;

        _slots = new Dictionary<MusicAppKind, AppSlot>
        {
            [MusicAppKind.Spotify] = new AppSlot { Kind = MusicAppKind.Spotify, DisplayName = "Spotify" },
            [MusicAppKind.YouTubeMusic] = new AppSlot { Kind = MusicAppKind.YouTubeMusic, DisplayName = "YouTube Music" }
        };

        try
        {
            _taskbarList = (ITaskbarList)new TaskbarListComObject();
            _taskbarList.HrInit();
        }
        catch
        {
            _taskbarList = null;
        }
    }

    public bool IsAppRunning(MusicAppKind kind)
    {
        if (kind == MusicAppKind.Spotify)
        {
            return HasNamedProcess("Spotify");
        }

        if (DedicatedYtMusicProcesses.Any(HasNamedProcess))
        {
            return true;
        }

        return FindMainWindows(MusicAppKind.YouTubeMusic).Count > 0;
    }

    public bool IsAnyWindowVisible(MusicAppKind kind)
    {
        foreach (var hwnd in FindMainWindows(kind))
        {
            if (IsWindowVisible(hwnd) && !IsIconic(hwnd))
            {
                return true;
            }
        }
        return false;
    }

    public async Task<(bool StartedOrPlaying, string StatusMessage)> LaunchAndPlayAsync(
        MusicAppKind kind,
        MediaPlayerService media,
        bool hideToTray,
        Action<string>? progressStatus = null)
    {
        if (_launchInFlight)
        {
            return (false, $"{_slots[kind].DisplayName} is starting...");
        }

        _launchInFlight = true;
        var slot = _slots[kind];
        bool preferYtMusic = kind == MusicAppKind.YouTubeMusic;

        try
        {
            if (await media.TryPlayMatchingAsync(preferYtMusic))
            {
                if (hideToTray)
                {
                    slot.UserRequestedVisible = false;
                    HideToTray(kind);
                }
                return (true, $"Playing {slot.DisplayName}.");
            }

            bool wasRunning = IsAppRunning(kind);
            if (!wasRunning)
            {
                slot.UserRequestedVisible = false;
                progressStatus?.Invoke($"Starting {slot.DisplayName}...");
                if (!StartAppProcess(kind))
                {
                    return (false, $"{slot.DisplayName} launcher not found on this PC.");
                }
            }

            var sw = Stopwatch.StartNew();
            long firstWindowMs = -1;
            long lastNudgeMs = -2000;

            while (sw.ElapsedMilliseconds < 12000 && !_disposed)
            {
                await Task.Delay(260);

                var windows = FindMainWindows(kind);
                if (windows.Count > 0 && firstWindowMs < 0)
                {
                    firstWindowMs = sw.ElapsedMilliseconds;
                }

                if (hideToTray && !slot.UserRequestedVisible && windows.Count > 0)
                {
                    HideWindows(slot, windows);
                }

                if (await media.TryPlayMatchingAsync(preferYtMusic))
                {
                    if (hideToTray)
                    {
                        slot.UserRequestedVisible = false;
                        await Task.Delay(220);
                        HideToTray(kind);
                    }
                    return (true, hideToTray ? $"Playing {slot.DisplayName} (hidden tray)." : $"Playing {slot.DisplayName}.");
                }

                bool readyToNudge = (windows.Count > 0 && sw.ElapsedMilliseconds - firstWindowMs >= 1000)
                                    || (wasRunning && sw.ElapsedMilliseconds >= 400);

                if (readyToNudge && sw.ElapsedMilliseconds - lastNudgeMs >= 2000)
                {
                    lastNudgeMs = sw.ElapsedMilliseconds;
                    TriggerPlay(windows, allowGlobalMediaKey: !media.HasAnySession);
                }
            }

            if (hideToTray && IsAppRunning(kind))
            {
                slot.UserRequestedVisible = false;
                HideToTray(kind);
                return (false, $"{slot.DisplayName} opened in hidden tray — pick a song to start.");
            }

            return (false, $"{slot.DisplayName} opened — pick a song or click Play once loaded.");
        }
        finally
        {
            _launchInFlight = false;
        }
    }

    public void HideToTray(MusicAppKind kind)
    {
        var slot = _slots[kind];
        if (!IsAppRunning(kind))
        {
            RemoveTrayIcon(slot);
            return;
        }

        slot.UserRequestedVisible = false;
        HideWindows(slot, FindMainWindows(kind));
        EnsureTrayIconVisible(slot);
    }

    public void RestoreWindow(MusicAppKind kind)
    {
        var slot = _slots[kind];
        slot.UserRequestedVisible = true;
        var windows = FindMainWindows(kind);

        if (windows.Count == 0 && !IsAppRunning(kind))
        {
            StartAppProcess(kind);
            return;
        }

        foreach (var hwnd in windows)
        {
            if (!IsWindow(hwnd)) continue;
            ShowWindow(hwnd, IsIconic(hwnd) ? SwRestore : SwShow);
            try { _taskbarList?.AddTab(hwnd); } catch { }
            SetForegroundWindow(hwnd);
        }

        EnsureTrayIconVisible(slot);
    }

    public void ToggleWindowVisibility(MusicAppKind kind)
    {
        if (!IsAppRunning(kind))
        {
            _slots[kind].UserRequestedVisible = true;
            StartAppProcess(kind);
            return;
        }

        if (IsAnyWindowVisible(kind))
        {
            HideToTray(kind);
        }
        else
        {
            RestoreWindow(kind);
        }
    }

    public void SyncAllStates(bool hideToTray)
    {
        if (_disposed) return;
        SyncSlotState(_slots[MusicAppKind.Spotify], hideToTray);
        SyncSlotState(_slots[MusicAppKind.YouTubeMusic], hideToTray);
    }

    private void SyncSlotState(AppSlot slot, bool hideToTray)
    {
        if (!IsAppRunning(slot.Kind))
        {
            slot.UserRequestedVisible = false;
            slot.KnownHwnds.Clear();
            RemoveTrayIcon(slot);
            return;
        }

        if (!hideToTray)
        {
            RemoveTrayIcon(slot);
            return;
        }

        EnsureTrayIconVisible(slot);

        var windows = FindMainWindows(slot.Kind);
        if (windows.Count == 0) return;

        IntPtr fg = GetForegroundWindow();
        bool anyMinimized = windows.Any(IsIconic);
        bool fgIsTarget = windows.Contains(fg);

        if (anyMinimized || (!slot.UserRequestedVisible && !fgIsTarget))
        {
            slot.UserRequestedVisible = false;
            HideWindows(slot, windows);
        }
    }

    public void QuitApp(MusicAppKind kind)
    {
        var slot = _slots[kind];
        if (kind == MusicAppKind.Spotify)
        {
            KillProcessesByName("Spotify");
        }
        else
        {
            // Close tracked YouTube Music --app windows safely without killing unrelated browser tabs
            foreach (var hwnd in FindMainWindows(MusicAppKind.YouTubeMusic))
            {
                if (IsWindow(hwnd))
                {
                    PostMessage(hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
                }
            }
            foreach (string procName in DedicatedYtMusicProcesses)
            {
                KillProcessesByName(procName);
            }
        }

        slot.UserRequestedVisible = false;
        slot.KnownHwnds.Clear();
        RemoveTrayIcon(slot);
    }

    private void HideWindows(AppSlot slot, IReadOnlyCollection<IntPtr> windows)
    {
        if (windows.Count == 0) return;
        foreach (var hwnd in windows)
        {
            if (!IsWindow(hwnd)) continue;
            try { _taskbarList?.DeleteTab(hwnd); } catch { }
            ShowWindow(hwnd, SwHide);
        }
        EnsureTrayIconVisible(slot);
    }

    private static void TriggerPlay(IReadOnlyCollection<IntPtr> windows, bool allowGlobalMediaKey)
    {
        foreach (var hwnd in windows)
        {
            if (!IsWindow(hwnd)) continue;
            PostMessage(hwnd, WmAppCommand, hwnd, (IntPtr)(AppCommandMediaPlay << 16));
            PostMessage(hwnd, WmAppCommand, hwnd, (IntPtr)(AppCommandMediaPlayPause << 16));

            IntPtr child = FindWindowEx(hwnd, IntPtr.Zero, "Chrome_RenderWidgetHostHWND", null);
            if (child != IntPtr.Zero)
            {
                PostMessage(child, WmAppCommand, child, (IntPtr)(AppCommandMediaPlay << 16));
                PostMessage(child, WmAppCommand, child, (IntPtr)(AppCommandMediaPlayPause << 16));
            }
        }

        if (allowGlobalMediaKey)
        {
            SendMediaPlayPauseKey();
        }
    }

    private static void SendMediaPlayPauseKey()
    {
        var inputs = new INPUT[2];
        inputs[0].type = InputKeyboard;
        inputs[0].u.ki = new KEYBDINPUT { wVk = VkMediaPlayPause };
        inputs[1].type = InputKeyboard;
        inputs[1].u.ki = new KEYBDINPUT { wVk = VkMediaPlayPause, dwFlags = KeyeventfKeyup };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
    }

    private static bool StartAppProcess(MusicAppKind kind)
    {
        return kind == MusicAppKind.Spotify ? StartSpotify() : StartYouTubeMusic();
    }

    private static bool StartSpotify()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] exeCandidates =
        [
            Path.Combine(localAppData, "Microsoft", "WindowsApps", "Spotify.exe"),
            Path.Combine(appData, "Spotify", "Spotify.exe")
        ];

        foreach (string exe in exeCandidates)
        {
            if (File.Exists(exe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(exe)
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    });
                    return true;
                }
                catch { }
            }
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", @"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify")
            {
                UseShellExecute = true
            });
            return true;
        }
        catch { }

        try
        {
            Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool StartYouTubeMusic()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        // 1. Standalone YouTube Music desktop apps (th-ch / YouTube Music Desktop App)
        string[] desktopApps =
        [
            Path.Combine(localAppData, "Programs", "YouTube Music", "YouTube Music.exe"),
            Path.Combine(localAppData, "Programs", "youtube-music", "YouTube Music.exe"),
            Path.Combine(localAppData, "Programs", "pear-desktop", "Pear Desktop.exe"),
            Path.Combine(appData, "YouTube Music Desktop App", "YouTube Music Desktop App.exe")
        ];
        foreach (string exe in desktopApps)
        {
            if (File.Exists(exe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(exe)
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    });
                    return true;
                }
                catch { }
            }
        }

        // 2. Installed Start Menu / Desktop YouTube Music PWA shortcut
        string[] shortcutRoots =
        [
            Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        ];
        foreach (string root in shortcutRoots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                string? lnk = Directory.EnumerateFiles(root, "*YouTube Music*.lnk", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(lnk))
                {
                    Process.Start(new ProcessStartInfo(lnk) { UseShellExecute = true });
                    return true;
                }
            }
            catch { }
        }

        // 3. Dedicated --app=https://music.youtube.com window in signed-in Chromium browser
        var browserCandidates = new List<(string Exe, string UserData, string ProgIdHint)>
        {
            (Path.Combine(progFiles, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
                Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"), "BraveHTML"),
            (Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
                Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"), "BraveHTML"),
            (Path.Combine(progFiles, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(localAppData, "Google", "Chrome", "User Data"), "ChromeHTML"),
            (Path.Combine(progFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(localAppData, "Google", "Chrome", "User Data"), "ChromeHTML"),
            (Path.Combine(progFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), "MSEdgeHTM"),
            (Path.Combine(progFiles, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), "MSEdgeHTM")
        };

        string? defaultProgId = ReadDefaultHttpsProgId();
        string? bestBrowserExe = browserCandidates
            .Where(c => File.Exists(c.Exe))
            .OrderByDescending(c => HasYouTubeMusicSiteData(c.UserData))
            .ThenByDescending(c => !string.IsNullOrEmpty(defaultProgId) && defaultProgId.Contains(c.ProgIdHint, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Exe)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(bestBrowserExe))
        {
            try
            {
                Process.Start(new ProcessStartInfo(bestBrowserExe, "--app=https://music.youtube.com")
                {
                    UseShellExecute = false
                });
                return true;
            }
            catch { }
        }

        // 4. Last fallback: default browser URL
        try
        {
            Process.Start(new ProcessStartInfo("https://music.youtube.com") { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadDefaultHttpsProgId()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
            return key?.GetValue("ProgId") as string;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasYouTubeMusicSiteData(string userDataDir)
    {
        if (!Directory.Exists(userDataDir)) return false;
        try
        {
            return Directory.EnumerateDirectories(userDataDir, "*music.youtube.com*", SearchOption.AllDirectories).Any();
        }
        catch
        {
            return false;
        }
    }

    private List<IntPtr> FindMainWindows(MusicAppKind kind)
    {
        var slot = _slots[kind];
        var result = new List<IntPtr>();
        var seen = new HashSet<IntPtr>();

        slot.KnownHwnds.RemoveWhere(h => !IsWindow(h));
        foreach (var h in slot.KnownHwnds)
        {
            if (seen.Add(h)) result.Add(h);
        }

        if (kind == MusicAppKind.Spotify)
        {
            var pids = CollectProcessIds("Spotify");
            if (pids.Count == 0) return result;

            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out int pid);
                if (!pids.Contains(pid)) return true;

                var cls = new StringBuilder(64);
                GetClassName(hwnd, cls, cls.Capacity);
                if (!cls.ToString().Equals("Chrome_WidgetWin_1", StringComparison.Ordinal)) return true;

                if (GetWindowTextLength(hwnd) > 0)
                {
                    slot.KnownHwnds.Add(hwnd);
                    if (seen.Add(hwnd)) result.Add(hwnd);
                }
                return true;
            }, IntPtr.Zero);
        }
        else
        {
            var dedicatedPids = new HashSet<int>();
            foreach (string name in DedicatedYtMusicProcesses)
            {
                dedicatedPids.UnionWith(CollectProcessIds(name));
            }

            var browserPids = new HashSet<int>();
            foreach (string name in ChromiumBrowserProcesses)
            {
                browserPids.UnionWith(CollectProcessIds(name));
            }

            if (dedicatedPids.Count == 0 && browserPids.Count == 0) return result;

            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out int pid);
                bool isDedicated = dedicatedPids.Contains(pid);
                bool isBrowser = browserPids.Contains(pid);
                if (!isDedicated && !isBrowser) return true;

                var cls = new StringBuilder(64);
                GetClassName(hwnd, cls, cls.Capacity);
                if (!cls.ToString().StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal)) return true;

                int len = GetWindowTextLength(hwnd);
                if (len <= 0) return true;

                var titleSb = new StringBuilder(len + 1);
                GetWindowText(hwnd, titleSb, titleSb.Capacity);
                string title = titleSb.ToString();

                if (isDedicated)
                {
                    slot.KnownHwnds.Add(hwnd);
                    if (seen.Add(hwnd)) result.Add(hwnd);
                    return true;
                }

                // Only target dedicated --app / PWA YouTube Music windows, never multi-tab browsers
                bool isStandaloneYtMusicTitle =
                    title.Contains("YouTube Music", StringComparison.OrdinalIgnoreCase) &&
                    !MultiTabBrowserTitleSuffixes.Any(s => title.EndsWith(s, StringComparison.OrdinalIgnoreCase));

                if (isStandaloneYtMusicTitle)
                {
                    slot.KnownHwnds.Add(hwnd);
                    if (seen.Add(hwnd)) result.Add(hwnd);
                }
                return true;
            }, IntPtr.Zero);
        }

        return result;
    }

    private static bool HasNamedProcess(string name)
    {
        try
        {
            var procs = Process.GetProcessesByName(name);
            bool any = procs.Length > 0;
            foreach (var p in procs) p.Dispose();
            return any;
        }
        catch
        {
            return false;
        }
    }

    private static HashSet<int> CollectProcessIds(string name)
    {
        var pids = new HashSet<int>();
        try
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                pids.Add(p.Id);
                p.Dispose();
            }
        }
        catch { }
        return pids;
    }

    private static void KillProcessesByName(string name)
    {
        try
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { p.Kill(); } catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }
    }

    private void EnsureTrayIconVisible(AppSlot slot)
    {
        if (_disposed) return;

        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => EnsureTrayIconVisible(slot));
            return;
        }

        if (slot.TrayIcon != null)
        {
            slot.TrayIcon.Visible = true;
            return;
        }

        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new AudioSwitch.DarkTrayMenuRenderer(),
            ShowImageMargin = false
        };

        var playItem = new Forms.ToolStripMenuItem("Play / Pause");
        playItem.Click += (_, _) => _ = _dispatcher.InvokeAsync(_onTogglePlayPause);

        var nextItem = new Forms.ToolStripMenuItem("Next Track");
        nextItem.Click += (_, _) => _ = _dispatcher.InvokeAsync(_onNextTrack);

        var prevItem = new Forms.ToolStripMenuItem("Previous Track");
        prevItem.Click += (_, _) => _ = _dispatcher.InvokeAsync(_onPreviousTrack);

        var showItem = new Forms.ToolStripMenuItem($"Show / Hide {slot.DisplayName}");
        showItem.Click += (_, _) => _dispatcher.BeginInvoke(() => ToggleWindowVisibility(slot.Kind));

        var quitItem = new Forms.ToolStripMenuItem($"Quit {slot.DisplayName}");
        quitItem.Click += (_, _) => _dispatcher.BeginInvoke(() => QuitApp(slot.Kind));

        menu.Items.Add(playItem);
        menu.Items.Add(nextItem);
        menu.Items.Add(prevItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(showItem);
        menu.Items.Add(quitItem);

        if (slot.IconHandle == null)
        {
            slot.IconHandle = CreateTrayIcon(slot.Kind, out IntPtr gdi);
            slot.IconGdiHandle = gdi;
        }

        slot.TrayIcon = new Forms.NotifyIcon
        {
            Icon = slot.IconHandle,
            Text = $"{slot.DisplayName} (Hidden Tray — Click to Show/Hide)",
            Visible = true,
            ContextMenuStrip = menu
        };

        slot.TrayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                _dispatcher.BeginInvoke(() => ToggleWindowVisibility(slot.Kind));
            }
        };
    }

    private void RemoveTrayIcon(AppSlot slot)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => RemoveTrayIcon(slot));
            return;
        }

        if (slot.TrayIcon != null)
        {
            slot.TrayIcon.Visible = false;
            slot.TrayIcon.Dispose();
            slot.TrayIcon = null;
        }
    }

    private static Drawing.Icon CreateTrayIcon(MusicAppKind kind, out IntPtr hIconOut)
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            if (kind == MusicAppKind.Spotify)
            {
                using var bgBrush = new Drawing.SolidBrush(Drawing.Color.FromArgb(29, 185, 84));
                g.FillEllipse(bgBrush, 1, 1, 30, 30);

                using var pen = new Drawing.Pen(Drawing.Color.FromArgb(11, 15, 25), 2.8f)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                g.DrawArc(pen, 6, 9, 20, 14, 215, 110);
                g.DrawArc(pen, 8, 14, 16, 12, 215, 110);
                g.DrawArc(pen, 10, 19, 12, 10, 215, 110);
            }
            else
            {
                using var bgBrush = new Drawing.SolidBrush(Drawing.Color.FromArgb(239, 68, 68));
                g.FillEllipse(bgBrush, 1, 1, 30, 30);

                using var ringPen = new Drawing.Pen(Drawing.Color.White, 2.0f);
                g.DrawEllipse(ringPen, 5, 5, 22, 22);

                using var triBrush = new Drawing.SolidBrush(Drawing.Color.White);
                Drawing.PointF[] tri =
                [
                    new Drawing.PointF(13.5f, 10.5f),
                    new Drawing.PointF(22.0f, 16.0f),
                    new Drawing.PointF(13.5f, 21.5f)
                ];
                g.FillPolygon(triBrush, tri);
            }
        }

        hIconOut = bmp.GetHicon();
        return Drawing.Icon.FromHandle(hIconOut);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var slot in _slots.Values)
        {
            if (slot.TrayIcon != null)
            {
                slot.TrayIcon.Visible = false;
                slot.TrayIcon.Dispose();
                slot.TrayIcon = null;
            }
            slot.IconHandle?.Dispose();
            slot.IconHandle = null;
            if (slot.IconGdiHandle != IntPtr.Zero)
            {
                DestroyIcon(slot.IconGdiHandle);
                slot.IconGdiHandle = IntPtr.Zero;
            }
        }
    }
}
