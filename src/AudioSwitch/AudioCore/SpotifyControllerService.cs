using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace AudioSwitch.AudioCore;

/// <summary>
/// Launches Spotify on demand, triggers playback reliably from cold start,
/// and keeps Spotify tucked into a Windows hidden tray icon instead of occupying taskbar space.
/// Consumes 0% CPU when idle (all work is strictly event-driven).
/// </summary>
internal sealed class SpotifyControllerService : IDisposable
{
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private const uint WmAppCommand = 0x0319;
    private const int AppCommandMediaPlayPause = 14;
    private const int AppCommandMediaPlay = 46;
    private const ushort VkMediaPlayPause = 0xB3;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;

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

    private readonly Dispatcher _dispatcher;
    private readonly Func<Task> _onTogglePlayPause;
    private readonly Func<Task> _onNextTrack;
    private readonly Func<Task> _onPreviousTrack;
    private readonly ITaskbarList? _taskbarList;
    private readonly HashSet<IntPtr> _knownSpotifyHwnds = new();

    private Forms.NotifyIcon? _spotifyTrayIcon;
    private Drawing.Icon? _trayIconHandle;
    private IntPtr _trayIconGdiHandle;
    private bool _launchInFlight;
    private bool _userRequestedVisible;
    private bool _disposed;

    public SpotifyControllerService(
        Dispatcher dispatcher,
        Func<Task> onTogglePlayPause,
        Func<Task> onNextTrack,
        Func<Task> onPreviousTrack)
    {
        _dispatcher = dispatcher;
        _onTogglePlayPause = onTogglePlayPause;
        _onNextTrack = onNextTrack;
        _onPreviousTrack = onPreviousTrack;

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

    public bool IsSpotifyRunning()
    {
        try
        {
            var procs = Process.GetProcessesByName("Spotify");
            bool running = procs.Length > 0;
            foreach (var p in procs) p.Dispose();
            return running;
        }
        catch
        {
            return false;
        }
    }

    public bool IsAnySpotifyWindowVisible()
    {
        foreach (var hwnd in FindSpotifyMainWindows())
        {
            if (IsWindowVisible(hwnd) && !IsIconic(hwnd))
            {
                return true;
            }
        }
        return false;
    }

    public async Task<(bool StartedOrPlaying, string StatusMessage)> LaunchAndPlayAsync(
        MediaPlayerService media,
        bool hideToTray,
        Action<string>? progressStatus = null)
    {
        if (_launchInFlight)
        {
            return (false, "Spotify is starting...");
        }

        _launchInFlight = true;
        try
        {
            // Fast path: SMTC already has a Spotify session registered.
            if (await media.TryPlaySpotifyAsync())
            {
                if (hideToTray)
                {
                    _userRequestedVisible = false;
                    HideSpotifyWindowsToTray();
                }
                return (true, "Playing Spotify.");
            }

            bool wasAlreadyRunning = IsSpotifyRunning();
            if (!wasAlreadyRunning)
            {
                _userRequestedVisible = false;
                progressStatus?.Invoke("Starting Spotify...");
                if (!StartSpotifyProcess())
                {
                    return (false, "Spotify executable not found on this PC.");
                }
            }

            var sw = Stopwatch.StartNew();
            long firstWindowMs = -1;
            long lastNudgeMs = -2000;

            while (sw.ElapsedMilliseconds < 12000 && !_disposed)
            {
                await Task.Delay(260);

                var windows = FindSpotifyMainWindows();
                if (windows.Count > 0 && firstWindowMs < 0)
                {
                    firstWindowMs = sw.ElapsedMilliseconds;
                }

                if (hideToTray && !_userRequestedVisible && windows.Count > 0)
                {
                    HideWindows(windows);
                }

                if (await media.TryPlaySpotifyAsync())
                {
                    if (hideToTray)
                    {
                        _userRequestedVisible = false;
                        await Task.Delay(220);
                        HideSpotifyWindowsToTray();
                    }
                    return (true, hideToTray ? "Playing Spotify (hidden tray)." : "Playing Spotify.");
                }

                bool readyToNudge = (windows.Count > 0 && sw.ElapsedMilliseconds - firstWindowMs >= 900)
                                    || (wasAlreadyRunning && sw.ElapsedMilliseconds >= 400);

                if (readyToNudge && sw.ElapsedMilliseconds - lastNudgeMs >= 2000)
                {
                    lastNudgeMs = sw.ElapsedMilliseconds;
                    TriggerSpotifyPlay(windows, allowGlobalMediaKey: !media.HasAnySession);
                }
            }

            if (hideToTray && IsSpotifyRunning())
            {
                _userRequestedVisible = false;
                HideSpotifyWindowsToTray();
                return (false, "Spotify opened in hidden tray. Pick a track to start.");
            }

            return (false, "Spotify started — click Play once a track is loaded.");
        }
        finally
        {
            _launchInFlight = false;
        }
    }

    public void HideSpotifyWindowsToTray()
    {
        if (!IsSpotifyRunning())
        {
            RemoveTrayIcon();
            return;
        }

        _userRequestedVisible = false;
        var windows = FindSpotifyMainWindows();
        HideWindows(windows);
        EnsureTrayIconVisible();
    }

    public void RestoreSpotifyWindow()
    {
        _userRequestedVisible = true;
        var windows = FindSpotifyMainWindows();
        if (windows.Count == 0 && !IsSpotifyRunning())
        {
            StartSpotifyProcess();
            return;
        }

        foreach (var hwnd in windows)
        {
            if (!IsWindow(hwnd)) continue;
            ShowWindow(hwnd, IsIconic(hwnd) ? SwRestore : SwShow);
            try { _taskbarList?.AddTab(hwnd); } catch { }
            SetForegroundWindow(hwnd);
        }

        EnsureTrayIconVisible();
    }

    public void ToggleSpotifyWindowVisibility()
    {
        if (!IsSpotifyRunning())
        {
            _userRequestedVisible = true;
            StartSpotifyProcess();
            return;
        }

        if (IsAnySpotifyWindowVisible())
        {
            HideSpotifyWindowsToTray();
        }
        else
        {
            RestoreSpotifyWindow();
        }
    }

    /// <summary>
    /// Called on event-driven hooks (foreground switches, SMTC events, settings updates).
    /// Tucks minimized or background Spotify cleanly into the hidden system tray with 0% idle polling.
    /// </summary>
    public void SyncState(bool hideToTray)
    {
        if (_disposed) return;

        if (!IsSpotifyRunning())
        {
            _userRequestedVisible = false;
            _knownSpotifyHwnds.Clear();
            RemoveTrayIcon();
            return;
        }

        if (!hideToTray)
        {
            RemoveTrayIcon();
            return;
        }

        EnsureTrayIconVisible();

        var windows = FindSpotifyMainWindows();
        if (windows.Count == 0) return;

        IntPtr fg = GetForegroundWindow();
        bool anyMinimized = windows.Any(IsIconic);
        bool fgIsSpotify = windows.Contains(fg);

        if (anyMinimized || (!_userRequestedVisible && !fgIsSpotify))
        {
            _userRequestedVisible = false;
            HideWindows(windows);
        }
    }

    public void QuitSpotify()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("Spotify"))
            {
                try { p.Kill(); } catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }

        _userRequestedVisible = false;
        _knownSpotifyHwnds.Clear();
        RemoveTrayIcon();
    }

    private void HideWindows(IReadOnlyCollection<IntPtr> windows)
    {
        if (windows.Count == 0) return;
        foreach (var hwnd in windows)
        {
            if (!IsWindow(hwnd)) continue;
            try { _taskbarList?.DeleteTab(hwnd); } catch { }
            ShowWindow(hwnd, SwHide);
        }
        EnsureTrayIconVisible();
    }

    private static void TriggerSpotifyPlay(IReadOnlyCollection<IntPtr> windows, bool allowGlobalMediaKey)
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

    private static bool StartSpotifyProcess()
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
                catch
                {
                    // Try next candidate
                }
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

    private List<IntPtr> FindSpotifyMainWindows()
    {
        var result = new List<IntPtr>();
        var seen = new HashSet<IntPtr>();
        var pids = new HashSet<int>();

        try
        {
            foreach (var p in Process.GetProcessesByName("Spotify"))
            {
                pids.Add(p.Id);
                p.Dispose();
            }
        }
        catch
        {
            return result;
        }

        // Preserve any previously tucked hidden HWNDs whose windows are still alive
        _knownSpotifyHwnds.RemoveWhere(h => !IsWindow(h));
        foreach (var h in _knownSpotifyHwnds)
        {
            if (seen.Add(h)) result.Add(h);
        }

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
                _knownSpotifyHwnds.Add(hwnd);
                if (seen.Add(hwnd)) result.Add(hwnd);
            }
            return true;
        }, IntPtr.Zero);

        return result;
    }

    private void EnsureTrayIconVisible()
    {
        if (_disposed) return;

        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(EnsureTrayIconVisible);
            return;
        }

        if (_spotifyTrayIcon != null)
        {
            _spotifyTrayIcon.Visible = true;
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

        var showItem = new Forms.ToolStripMenuItem("Show / Hide Spotify");
        showItem.Click += (_, _) => _dispatcher.BeginInvoke(ToggleSpotifyWindowVisibility);

        var quitItem = new Forms.ToolStripMenuItem("Quit Spotify");
        quitItem.Click += (_, _) => _dispatcher.BeginInvoke(QuitSpotify);

        menu.Items.Add(playItem);
        menu.Items.Add(nextItem);
        menu.Items.Add(prevItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(showItem);
        menu.Items.Add(quitItem);

        _trayIconHandle ??= CreateSpotifyTrayIcon(out _trayIconGdiHandle);

        _spotifyTrayIcon = new Forms.NotifyIcon
        {
            Icon = _trayIconHandle,
            Text = "Spotify (Hidden Tray — Click to Show/Hide)",
            Visible = true,
            ContextMenuStrip = menu
        };

        _spotifyTrayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                _dispatcher.BeginInvoke(ToggleSpotifyWindowVisibility);
            }
        };
    }

    private void RemoveTrayIcon()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(RemoveTrayIcon);
            return;
        }

        if (_spotifyTrayIcon != null)
        {
            _spotifyTrayIcon.Visible = false;
            _spotifyTrayIcon.Dispose();
            _spotifyTrayIcon = null;
        }
    }

    private static Drawing.Icon CreateSpotifyTrayIcon(out IntPtr hIconOut)
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

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

        hIconOut = bmp.GetHicon();
        return Drawing.Icon.FromHandle(hIconOut);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_spotifyTrayIcon != null)
        {
            _spotifyTrayIcon.Visible = false;
            _spotifyTrayIcon.Dispose();
            _spotifyTrayIcon = null;
        }

        _trayIconHandle?.Dispose();
        _trayIconHandle = null;
        if (_trayIconGdiHandle != IntPtr.Zero)
        {
            DestroyIcon(_trayIconGdiHandle);
            _trayIconGdiHandle = IntPtr.Zero;
        }
    }
}
