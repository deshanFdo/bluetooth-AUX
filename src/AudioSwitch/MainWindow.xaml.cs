using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AudioSwitch.AudioCore;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace AudioSwitch;

public partial class MainWindow : Window
{
    private enum IslandState
    {
        ThinLine,
        CompactPill,
        ExpandedDeck
    }

    private enum IslandPage
    {
        Audio = 0,
        Media = 1,
        Battery = 2
    }

    private const double LineWidth = 120;
    private const double LineHeight = 14;
    private const double PillWidth = 282;
    private const double PillHeight = 40;
    private const double ExpandedWidth = 348;

    private readonly AudioRouterService _router = new();
    private readonly AudioMirrorService _mirror = new();
    private readonly BackgroundPeakSampler _peakSampler = new();
    private readonly SystemStatusService _telemetry = new();
    private readonly MediaPlayerService _media = new();
    private IslandSettings _settings = new();
    private bool _isCustomizing;
    private IslandPage _activePage = IslandPage.Audio;
    private Point _pillPressPoint;
    private bool _pillPressTracking;
    private bool _swipeTrackingDeck;
    private bool _swipeDragMoved;
    private long _lastWheelSwipeTicks;

    private readonly DispatcherTimer _meterTimer = new();
    private readonly DispatcherTimer _collapseTimer = new();
    private readonly DispatcherTimer _refreshDebounceTimer = new();
    private readonly DispatcherTimer _telemetryTimer = new();
    private readonly DispatcherTimer _mediaProgressTimer = new();
    private Forms.NotifyIcon? _trayIcon;
    private Drawing.Icon? _trayIconHandle;

    private sealed class BtRingItem
    {
        public required string Name { get; init; }
        public required double Percent { get; init; }
        public required string PercentText { get; init; }
    }

    private MediaSessionSnapshot _lastMedia = new(false, string.Empty, string.Empty, string.Empty, false, null, TimeSpan.Zero, TimeSpan.Zero, DateTimeOffset.MinValue, 1.0);
    private bool _telemetryRefreshInFlight;
    private Color _currentAudioAccent = Color.FromRgb(0x3B, 0x82, 0xF6);
    private string _lastRouteIcon = "🎧";
    private string _lastRouteLabel = "AUX";

    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private struct SpringValue
    {
        public double Value;
        public double Target;
        public double Velocity;

        public bool Step(double dt, double stiffness, double damping)
        {
            double force = -stiffness * (Value - Target);
            double accel = force - damping * Velocity;
            Velocity += accel * dt;
            Value += Velocity * dt;

            if (Math.Abs(Target - Value) < 0.2 && Math.Abs(Velocity) < 1.2)
            {
                Value = Target;
                Velocity = 0;
                return true;
            }
            return false;
        }
    }

    private SpringValue _springW = new() { Value = LineWidth, Target = LineWidth };
    private SpringValue _springH = new() { Value = LineHeight, Target = LineHeight };
    private SpringValue _springGlass = new() { Value = 0, Target = 0 };
    private SpringValue _springMarginTop = new() { Value = 2, Target = 2 };
    private SpringValue _springBounce = new() { Value = 1.0, Target = 1.0 };
    private SpringValue _springWinLeft;
    private SpringValue _springWinTop;
    private bool _animateWindowPos;
    private bool _springLoopHooked;
    private long _lastRenderTicks;

    private sealed class MultiOutputOption
    {
        public required string Id { get; init; }
        public required AudioCategory Category { get; init; }
        public required string ShortLabel { get; init; }
        public required string DisplayLabel { get; init; }
        public bool IsCheckable { get; init; }
        public bool IsChecked { get; set; }
        public bool IsMuted { get; init; }
        public System.Windows.Media.Brush TextBrush { get; init; } = System.Windows.Media.Brushes.White;
    }

    private readonly HashSet<string> _multiSelectedIds = new(StringComparer.OrdinalIgnoreCase);
    private List<MultiOutputOption> _multiOptions = new();
    private int _btWiredDelayMs = 170;
    private int _wiredDelayMs = 0;
    private bool _mirrorHasBtAndWired;

    private IslandState _state = IslandState.ThinLine;
    private bool _isUpdatingUi;
    private bool _isPinnedOpen;
    private bool _isMicTabActive;
    private bool _isDragging;
    private bool _beaconRunning;

    // Output (Render) state
    private AudioEndpointInfo? _auxEndpoint;
    private AudioEndpointInfo? _speakersEndpoint;
    private AudioEndpointInfo? _selectedBtEndpoint;
    private List<AudioEndpointInfo> _btEndpoints = new();
    private string? _preferredBtId;

    // Mic (Capture) state
    private AudioEndpointInfo? _auxMicEndpoint;
    private AudioEndpointInfo? _builtInMicEndpoint;
    private AudioEndpointInfo? _selectedBtMicEndpoint;
    private List<AudioEndpointInfo> _btMicEndpoints = new();
    private string? _preferredBtMicId;
    private AudioEndpointInfo? _selectedOtherMicEndpoint;
    private List<AudioEndpointInfo> _otherMicEndpoints = new();
    private string? _preferredOtherMicId;

    // Smoothed meter values
    private double _smoothedOutLevel;
    private double _smoothedActiveMic;

    public MainWindow()
    {
        InitializeComponent();
        _router.AudioStateChanged += OnAudioStateChanged;

        _meterTimer.Interval = TimeSpan.FromMilliseconds(28);
        _meterTimer.Tick += MeterTimer_Tick;

        _collapseTimer.Interval = TimeSpan.FromMilliseconds(240);
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (!ShouldStayVisible())
            {
                CollapseToThinLine();
            }
        };

        _refreshDebounceTimer.Interval = TimeSpan.FromMilliseconds(55);
        _refreshDebounceTimer.Tick += (_, _) =>
        {
            _refreshDebounceTimer.Stop();
            RefreshDevices();
        };

        _telemetryTimer.Interval = TimeSpan.FromSeconds(2);
        _telemetryTimer.Tick += (_, _) => _ = RefreshTelemetryAsync();

        _mediaProgressTimer.Interval = TimeSpan.FromMilliseconds(500);
        _mediaProgressTimer.Tick += (_, _) => UpdateMediaProgressVisuals();

        _media.MediaUpdated += snap => Dispatcher.BeginInvoke(() => UpdateMediaUi(snap));

        Loaded += async (_, _) =>
        {
            _settings = IslandSettings.Load();
            SyncCheckboxesFromSettings();
            ApplySettingsToUi();
            PositionTopCenter();
            SetupTrayIcon();
            RefreshDevices();
            // Keep meters, telemetry, & beacon animations stopped while resting as ThinLine (0% idle CPU)
            AnimateInitialLineEntrance();
            await _media.InitializeAsync();
        };

        Deactivated += (_, _) =>
        {
            if (_isDragging || _isPinnedOpen) return;
            if (BtDeviceSelector.IsDropDownOpen || BtMicSelector.IsDropDownOpen || OtherMicSelector.IsDropDownOpen) return;
            _collapseTimer.Stop();
            CollapseToThinLine();
        };

        Closed += (_, _) =>
        {
            StopSpringLoop();
            _meterTimer.Stop();
            _collapseTimer.Stop();
            _refreshDebounceTimer.Stop();
            _telemetryTimer.Stop();
            _mediaProgressTimer.Stop();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _trayIconHandle?.Dispose();
            _trayIconHandle = null;
            _media.Dispose();
            _peakSampler.Dispose();
            _mirror.Dispose();
            _router.AudioStateChanged -= OnAudioStateChanged;
            _router.Dispose();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            long style = GetWindowLongPtr64(hwnd, GwlExStyle).ToInt64();
            SetWindowLongPtr64(hwnd, GwlExStyle, new IntPtr(style | WsExToolWindow));
        }
    }

    private void SetupTrayIcon()
    {
        try
        {
            var res = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/AudioSwitch.ico"));
            if (res?.Stream != null)
            {
                using (res.Stream)
                {
                    _trayIconHandle = new Drawing.Icon(res.Stream);
                }
            }
        }
        catch
        {
            // Fallback below
        }

        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new DarkTrayMenuRenderer(),
            ShowImageMargin = false
        };

        var openItem = new Forms.ToolStripMenuItem("Open Audio Island");
        openItem.Click += (_, _) => Dispatcher.Invoke(ExpandIslandFromWake);

        var swapItem = new Forms.ToolStripMenuItem("Quick Swap (AUX <-> Bluetooth)");
        swapItem.Click += (_, _) => Dispatcher.Invoke(() => SwapButton_Click(this, new RoutedEventArgs()));

        var collapseItem = new Forms.ToolStripMenuItem("Collapse to Top Line");
        collapseItem.Click += (_, _) => Dispatcher.Invoke(CollapseToThinLine);

        var centerItem = new Forms.ToolStripMenuItem("Recenter at Top");
        centerItem.Click += (_, _) => Dispatcher.Invoke(PositionTopCenter);

        var exitItem = new Forms.ToolStripMenuItem("Exit Audio Switch");
        exitItem.Click += (_, _) => Dispatcher.Invoke(Close);

        menu.Items.Add(openItem);
        menu.Items.Add(swapItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(collapseItem);
        menu.Items.Add(centerItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _trayIconHandle ?? Drawing.SystemIcons.Application,
            Text = "Audio Switch",
            Visible = true,
            ContextMenuStrip = menu
        };

        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                Dispatcher.Invoke(() =>
                {
                    if (_state == IslandState.ExpandedDeck)
                    {
                        CollapseToThinLine();
                    }
                    else
                    {
                        ExpandIslandFromWake();
                    }
                });
            }
        };
    }

    private sealed class DarkTrayMenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        public DarkTrayMenuRenderer() : base(new DarkTrayColorTable()) { }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Drawing.Color.FromArgb(241, 245, 249);
            base.OnRenderItemText(e);
        }
    }

    private sealed class DarkTrayColorTable : Forms.ProfessionalColorTable
    {
        public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.FromArgb(20, 26, 40);
        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(51, 65, 92);
        public override Drawing.Color MenuItemSelected => Drawing.Color.FromArgb(37, 99, 235);
        public override Drawing.Color MenuItemBorder => Drawing.Color.FromArgb(59, 130, 246);
        public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(45, 55, 78);
        public override Drawing.Color SeparatorLight => Drawing.Color.FromArgb(45, 55, 78);
    }

    public void ExpandIslandFromWake()
    {
        Topmost = true;
        ExpandToDeck();
    }

    private void PositionTopCenter()
    {
        _animateWindowPos = false;
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2.0;
        Top = workArea.Top;
        _springWinLeft = new SpringValue { Value = Left, Target = Left };
        _springWinTop = new SpringValue { Value = Top, Target = Top };
    }

    private void SnapToCurrentScreenTopCenter(bool animate)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var screen = hwnd != IntPtr.Zero
            ? Forms.Screen.FromHandle(hwnd)
            : Forms.Screen.PrimaryScreen;

        if (screen == null)
        {
            PositionTopCenter();
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        double targetLeft = (screen.WorkingArea.Left / scaleX) + ((screen.WorkingArea.Width / scaleX) - Width) / 2.0;
        double targetTop = screen.WorkingArea.Top / scaleY;

        if (!animate || (Math.Abs(Left - targetLeft) < 2 && Math.Abs(Top - targetTop) < 2))
        {
            _animateWindowPos = false;
            Left = targetLeft;
            Top = targetTop;
            _springWinLeft = new SpringValue { Value = Left, Target = Left };
            _springWinTop = new SpringValue { Value = Top, Target = Top };
            return;
        }

        _springWinLeft.Value = Left;
        _springWinLeft.Target = targetLeft;
        _springWinTop.Value = Top;
        _springWinTop.Target = targetTop;
        _animateWindowPos = true;
        StartSpringLoop();
    }

    private void AnimateInitialLineEntrance()
    {
        ThinLineBar.Opacity = 0;
        IslandTranslate.Y = -8;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ThinLineBar.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 0.88, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        IslandTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-8, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
    }

    // --- 3-Stage VSync iOS Spring Dynamic Island Engine ---
    private void StartSpringLoop()
    {
        if (_springLoopHooked) return;
        _lastRenderTicks = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += OnSpringFrame;
        _springLoopHooked = true;
    }

    private void StopSpringLoop()
    {
        if (!_springLoopHooked) return;
        CompositionTarget.Rendering -= OnSpringFrame;
        _springLoopHooked = false;
    }

    private void OnSpringFrame(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double dt = (now - _lastRenderTicks) / (double)Stopwatch.Frequency;
        _lastRenderTicks = now;
        if (dt <= 0) dt = 1.0 / 60.0;
        dt = Math.Min(dt, 0.032);

        // Step springs in sub-steps for crisp stability at any refresh rate (60Hz / 120Hz / 144Hz)
        int subSteps = dt > 0.012 ? 2 : 1;
        double subDt = dt / subSteps;

        bool wDone = false, hDone = false, gDone = false, mDone = false, bDone = false;
        bool posDone = !_animateWindowPos;

        for (int i = 0; i < subSteps; i++)
        {
            wDone = _springW.Step(subDt, stiffness: 390, damping: 29.5);
            hDone = _springH.Step(subDt, stiffness: 370, damping: 30.5);
            gDone = _springGlass.Step(subDt, stiffness: 420, damping: 41.0);
            mDone = _springMarginTop.Step(subDt, stiffness: 400, damping: 40.0);
            bDone = _springBounce.Step(subDt, stiffness: 520, damping: 28.0);
            if (_animateWindowPos)
            {
                bool lx = _springWinLeft.Step(subDt, stiffness: 360, damping: 31.0);
                bool ty = _springWinTop.Step(subDt, stiffness: 360, damping: 31.0);
                posDone = lx && ty;
            }
        }

        double w = Math.Max(40, _springW.Value);
        double h = Math.Max(10, _springH.Value);
        IslandShell.Width = w;
        if (!double.IsNaN(IslandShell.Height))
        {
            IslandShell.Height = h;
        }

        double corner = _state == IslandState.ExpandedDeck
            ? Math.Clamp(h * 0.5, 20, 26)
            : Math.Clamp(h * 0.5, 7, 20);
        IslandShell.CornerRadius = new CornerRadius(corner);
        IslandShell.Margin = new Thickness(0, Math.Max(0, _springMarginTop.Value), 0, 18);
        UpdateRoundedInnerClip(corner);

        double g = Math.Clamp(_springGlass.Value, 0, 1);
        GlassSurface.Opacity = g;
        GlassSheen.Opacity = g * 0.65;
        AmbientGlassGlow.Opacity = g * 0.24;
        TopSpecularSheen.Opacity = g;

        if (_state == IslandState.ThinLine)
        {
            ThinLineView.Visibility = Visibility.Visible;
            ThinLineView.Opacity = Math.Clamp(1.0 - g, 0, 1);
        }

        IslandScale.ScaleX = _springBounce.Value;
        IslandScale.ScaleY = _springBounce.Value;

        if (_animateWindowPos)
        {
            Left = _springWinLeft.Value;
            Top = _springWinTop.Value;
            if (posDone)
            {
                _animateWindowPos = false;
            }
        }

        if (wDone && hDone && gDone && mDone && bDone && posDone)
        {
            StopSpringLoop();
            if (_state == IslandState.ExpandedDeck)
            {
                IslandShell.Height = double.NaN;
            }
            else if (_state == IslandState.ThinLine)
            {
                ExpandedDeckView.Visibility = Visibility.Collapsed;
                CompactPillView.Visibility = Visibility.Collapsed;
                ThinLineView.Visibility = Visibility.Visible;
                IslandShell.BorderThickness = new Thickness(0);
            }
        }
    }

    private void IslandContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double radius = _state == IslandState.ExpandedDeck ? 26 : IslandShell.CornerRadius.TopLeft;
        UpdateRoundedInnerClip(radius);
    }

    private void UpdateRoundedInnerClip(double outerCorner)
    {
        double cw = IslandContentGrid.ActualWidth;
        double ch = IslandContentGrid.ActualHeight;
        if (cw <= 0 || ch <= 0) return;

        double innerR = Math.Max(0, outerCorner - IslandShell.BorderThickness.Left);
        var innerRadius = new CornerRadius(innerR);
        GlassSurface.CornerRadius = innerRadius;
        GlassSheen.CornerRadius = innerRadius;
        AmbientGlassGlow.CornerRadius = innerRadius;
        IslandContentGrid.Clip = new RectangleGeometry(new Rect(0, 0, cw, ch), innerR, innerR);
    }

    private void IslandShell_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _collapseTimer.Stop();
        if (_state == IslandState.ThinLine)
        {
            RevealCompactPill();
        }
    }

    private void IslandShell_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!ShouldStayVisible())
        {
            _collapseTimer.Stop();
            _collapseTimer.Start();
        }
    }

    private bool ShouldStayVisible()
    {
        if (_isPinnedOpen || _isDragging) return true;
        if (IslandShell.IsMouseOver) return true;
        if (BtDeviceSelector.IsDropDownOpen || BtMicSelector.IsDropDownOpen || OtherMicSelector.IsDropDownOpen) return true;
        if (Mouse.LeftButton == MouseButtonState.Pressed && Mouse.Captured != null) return true;
        return false;
    }

    private static DoubleAnimation SmoothAnim(double to, int ms, IEasingFunction? ease = null)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = ease ?? new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Timeline.SetDesiredFrameRate(anim, 120);
        return anim;
    }

    private static DoubleAnimation SmoothAnim(double from, double to, int ms, IEasingFunction? ease = null)
    {
        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = ease ?? new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Timeline.SetDesiredFrameRate(anim, 120);
        return anim;
    }

    private void RevealCompactPill()
    {
        if (_state == IslandState.CompactPill || _state == IslandState.ExpandedDeck) return;
        _state = IslandState.CompactPill;
        _collapseTimer.Stop();
        UpdatePeakSamplerTargets();
        _ = RefreshTelemetryAsync();

        double curH = double.IsNaN(IslandShell.Height) ? IslandShell.ActualHeight : IslandShell.Height;
        if (curH <= 0) curH = LineHeight;
        _springW.Value = IslandShell.ActualWidth > 0 ? IslandShell.ActualWidth : _springW.Value;
        _springH.Value = curH;
        IslandShell.Height = curH;

        IslandShell.BorderThickness = new Thickness(1);
        IslandShell.Background = new SolidColorBrush(Color.FromArgb(0x02, 0, 0, 0));

        ThinLineView.Visibility = Visibility.Collapsed;
        ExpandedDeckView.Visibility = Visibility.Collapsed;
        CompactPillView.Visibility = Visibility.Visible;
        CompactPillView.BeginAnimation(OpacityProperty, SmoothAnim(0, 1, 140, new QuarticEase { EasingMode = EasingMode.EaseOut }));

        _springW.Target = PillWidth;
        _springH.Target = PillHeight;
        _springGlass.Target = 1.0;
        _springMarginTop.Target = 6.0;
        _springBounce.Velocity = 0.24;
        StartSpringLoop();
    }

    private void ExpandToDeck()
    {
        if (_state == IslandState.ExpandedDeck) return;
        _state = IslandState.ExpandedDeck;
        _collapseTimer.Stop();
        Activate();
        UpdatePeakSamplerTargets();
        _ = RefreshTelemetryAsync();

        IslandShell.BorderThickness = new Thickness(1);
        ThinLineView.Visibility = Visibility.Collapsed;
        CompactPillView.Visibility = Visibility.Collapsed;
        ExpandedDeckView.Visibility = Visibility.Visible;

        ExpandedDeckView.Measure(new Size(ExpandedWidth - 2, double.PositiveInfinity));
        double targetHeight = Math.Clamp(ExpandedDeckView.DesiredSize.Height + 6, 170, 520);
        UpdatePageIndicatorVisuals();

        double curH = double.IsNaN(IslandShell.Height) ? IslandShell.ActualHeight : IslandShell.Height;
        if (curH <= 0) curH = PillHeight;
        _springW.Value = IslandShell.ActualWidth > 0 ? IslandShell.ActualWidth : _springW.Value;
        _springH.Value = curH;
        IslandShell.Height = curH;

        _springW.Target = ExpandedWidth;
        _springH.Target = targetHeight;
        _springGlass.Target = 1.0;
        _springMarginTop.Target = 6.0;
        _springBounce.Velocity = 0.22;
        StartSpringLoop();

        var quart = new QuarticEase { EasingMode = EasingMode.EaseOut };
        ExpandedDeckView.BeginAnimation(OpacityProperty, SmoothAnim(0, 1, 175, quart));
        ExpandedSlide.BeginAnimation(TranslateTransform.YProperty, SmoothAnim(-10, 0, 200, quart));
    }

    private void CollapseToThinLine()
    {
        SnapToCurrentScreenTopCenter(animate: true);
        if (_isCustomizing) ToggleCustomizeView(false);
        if (_state == IslandState.ThinLine) return;
        _state = IslandState.ThinLine;
        _collapseTimer.Stop();
        UpdatePeakSamplerTargets();

        double curH = double.IsNaN(IslandShell.Height) ? IslandShell.ActualHeight : IslandShell.Height;
        if (curH <= 0) curH = PillHeight;
        _springW.Value = IslandShell.ActualWidth > 0 ? IslandShell.ActualWidth : _springW.Value;
        _springH.Value = curH;
        IslandShell.Height = curH;

        var quart = new QuarticEase { EasingMode = EasingMode.EaseOut };
        ExpandedDeckView.BeginAnimation(OpacityProperty, SmoothAnim(0, 95, quart));
        CompactPillView.BeginAnimation(OpacityProperty, SmoothAnim(0, 95, quart));

        _springW.Target = LineWidth;
        _springH.Target = LineHeight;
        _springGlass.Target = 0.0;
        _springMarginTop.Target = 2.0;
        _springBounce.Velocity = -0.14;
        StartSpringLoop();
    }

    private void UpdatePeakSamplerTargets()
    {
        if (_state == IslandState.ThinLine)
        {
            _meterTimer.Stop();
            _telemetryTimer.Stop();
            _mediaProgressTimer.Stop();
            if (_beaconRunning && Resources["BeaconPulseStoryboard"] is Storyboard pulse)
            {
                pulse.Stop(this);
                _beaconRunning = false;
            }
            _peakSampler.SetTargets(null, null, enabled: false);
            return;
        }

        if (!_meterTimer.IsEnabled)
        {
            _meterTimer.Start();
        }
        if (_state == IslandState.ExpandedDeck && !_telemetryTimer.IsEnabled)
        {
            _telemetryTimer.Start();
        }
        else if (_state != IslandState.ExpandedDeck && _telemetryTimer.IsEnabled)
        {
            _telemetryTimer.Stop();
        }
        UpdateMediaProgressState();
        if (!_beaconRunning && Resources["BeaconPulseStoryboard"] is Storyboard activePulse)
        {
            activePulse.Begin(this, true);
            _beaconRunning = true;
        }

        var activeOut = (_auxEndpoint is { IsDefaultPlayback: true } ? _auxEndpoint : null)
                        ?? (_selectedBtEndpoint is { IsDefaultPlayback: true } ? _selectedBtEndpoint : null)
                        ?? (_speakersEndpoint is { IsDefaultPlayback: true } ? _speakersEndpoint : null);

        string? outId = (activeOut is { IsActive: true, IsMuted: false }) ? activeOut.Id : null;
        var activeMic = GetActiveCaptureEndpoint();
        string? micId = (activeMic is { IsActive: true, IsMuted: false }) ? activeMic.Id : null;
        _peakSampler.SetTargets(outId, micId, enabled: true);
    }

    private void OnAudioStateChanged()
    {
        Dispatcher.BeginInvoke(() =>
        {
            _refreshDebounceTimer.Stop();
            _refreshDebounceTimer.Start();
        });
    }

    private void MeterTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible || _state == IslandState.ThinLine) return;

        double rawOut = _peakSampler.LatestOutPeak;
        _smoothedOutLevel = SmoothLevel(_smoothedOutLevel, rawOut);

        if (_state == IslandState.CompactPill)
        {
            double level = _smoothedOutLevel;
            EqBar1.ScaleY = 0.22 + Math.Min(0.78, level * 1.15);
            EqBar2.ScaleY = 0.28 + Math.Min(0.72, Math.Pow(level, 0.85) * 1.05);
            EqBar3.ScaleY = 0.20 + Math.Min(0.80, level * 0.92);
            return;
        }

        if (!_isMicTabActive)
        {
            AuxMeterScale.ScaleX = (_auxEndpoint is { IsActive: true, IsDefaultPlayback: true, IsMuted: false }) ? _smoothedOutLevel : 0.0;
            BtMeterScale.ScaleX = (_selectedBtEndpoint is { IsActive: true, IsDefaultPlayback: true, IsMuted: false }) ? _smoothedOutLevel : 0.0;
        }
        else
        {
            var activeMic = GetActiveCaptureEndpoint();
            double targetMic = _peakSampler.LatestMicPeak;
            _smoothedActiveMic = SmoothLevel(_smoothedActiveMic, targetMic);
            ActiveMicMeterScale.ScaleX = _smoothedActiveMic;
            ActiveMicDbLabel.Text = activeMic == null
                ? "None"
                : activeMic.IsMuted
                    ? "Muted"
                    : $"{(int)Math.Round(_smoothedActiveMic * 100)}%";
        }
    }

    private static double SmoothLevel(double current, double target)
    {
        double factor = target > current ? 0.42 : 0.16;
        double next = current + (target - current) * factor;
        return next < 0.004 ? 0.0 : Math.Min(1.0, next);
    }

    private AudioEndpointInfo? GetActiveCaptureEndpoint()
    {
        if (_auxMicEndpoint is { IsDefaultPlayback: true }) return _auxMicEndpoint;
        if (_selectedBtMicEndpoint is { IsDefaultPlayback: true }) return _selectedBtMicEndpoint;
        if (_builtInMicEndpoint is { IsDefaultPlayback: true }) return _builtInMicEndpoint;
        if (_selectedOtherMicEndpoint is { IsDefaultPlayback: true }) return _selectedOtherMicEndpoint;

        return _btMicEndpoints.FirstOrDefault(e => e.IsDefaultPlayback)
               ?? _otherMicEndpoints.FirstOrDefault(e => e.IsDefaultPlayback);
    }

    private void RefreshDevices()
    {
        _isUpdatingUi = true;
        try
        {
            var renders = _router.GetRenderEndpoints();
            var captures = _router.GetCaptureEndpoints();

            LoadOutputs(renders);
            LoadMicrophones(captures);
            UpdateHeaderAndPill(renders, captures);
            UpdatePeakSamplerTargets();
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void LoadOutputs(IReadOnlyList<AudioEndpointInfo> all)
    {
        _auxEndpoint = all
            .Where(e => e.Category == AudioCategory.AuxWired)
            .OrderByDescending(e => e.IsActive)
            .ThenByDescending(e => e.IsDefaultPlayback)
            .FirstOrDefault();

        _speakersEndpoint = all
            .Where(e => e.Category == AudioCategory.BuiltInSpeakers)
            .OrderByDescending(e => e.IsActive)
            .ThenByDescending(e => e.IsDefaultPlayback)
            .FirstOrDefault();

        var rawBt = all
            .Where(e => e.Category == AudioCategory.Bluetooth)
            .OrderByDescending(e => e.IsActive)
            .ThenBy(e => e.IsHandsFree)
            .ThenByDescending(e => e.IsDefaultPlayback)
            .ToList();

        var stereoBt = rawBt.Where(e => !e.IsHandsFree).ToList();
        _btEndpoints = stereoBt.Count > 0 ? stereoBt : rawBt;

        if (!string.IsNullOrEmpty(_preferredBtId))
        {
            _selectedBtEndpoint = _btEndpoints.FirstOrDefault(e => e.Id == _preferredBtId);
        }
        _selectedBtEndpoint ??= _btEndpoints.FirstOrDefault();
        if (_selectedBtEndpoint != null)
        {
            _preferredBtId = _selectedBtEndpoint.Id;
        }

        UpdateAuxCard();
        UpdateBluetoothCard();
        UpdateSpeakersCard();
        UpdateMultiOutputList(all);
    }

    private void UpdateMultiOutputList(IReadOnlyList<AudioEndpointInfo> all)
    {
        var candidates = all
            .Where(e => !e.IsVirtual && !(e.Category == AudioCategory.Bluetooth && e.IsHandsFree))
            .OrderByDescending(e => e.IsActive)
            .ThenByDescending(e => e.IsDefaultPlayback)
            .ToList();

        // Keep selected set synced with currently active endpoints if mirroring is active
        if (_mirror.IsRunning)
        {
            var activeIds = candidates.Where(c => c.IsActive).Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _multiSelectedIds.RemoveWhere(id => !activeIds.Contains(id));
            if (_multiSelectedIds.Count < 2)
            {
                _mirror.StopMirroring();
            }
        }
        else
        {
            _multiSelectedIds.Clear();
            var def = candidates.FirstOrDefault(c => c.IsActive && c.IsDefaultPlayback);
            if (def != null) _multiSelectedIds.Add(def.Id);
        }

        _multiOptions = candidates.Select(ep =>
        {
            string tag = ep.Category switch
            {
                AudioCategory.Bluetooth => "BT",
                AudioCategory.AuxWired => "AUX",
                AudioCategory.BuiltInSpeakers => "Speakers",
                _ => "Out"
            };

            string suffix = ep.IsActive
                ? (ep.IsDefaultPlayback ? " • Primary" : " • Ready")
                : (ep.Category == AudioCategory.Bluetooth ? " • Disconnected (1 BT A2DP limit / off)" : $" • {ep.StateLabel}");

            return new MultiOutputOption
            {
                Id = ep.Id,
                Category = ep.Category,
                ShortLabel = tag == "BT" ? ep.ShortName : tag,
                DisplayLabel = $"[{tag}] {ep.FriendlyName}{suffix}",
                IsCheckable = ep.IsActive,
                IsChecked = ep.IsActive && _multiSelectedIds.Contains(ep.Id),
                IsMuted = ep.IsMuted,
                TextBrush = ep.IsActive
                    ? new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9))
                    : new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
            };
        }).ToList();

        MultiOutputCard.Visibility = _settings.ShowMultiOutput ? Visibility.Visible : Visibility.Collapsed;
        MultiOutputItems.ItemsSource = _multiOptions;
        bool mirroring = _mirror.IsRunning;
        MultiBadge.Visibility = mirroring ? Visibility.Visible : Visibility.Collapsed;
        StopMultiBtn.Visibility = mirroring ? Visibility.Visible : Visibility.Collapsed;
        SyncDelayRow.Visibility = mirroring ? Visibility.Visible : Visibility.Collapsed;
        if (mirroring)
        {
            SyncDelaySlider.Value = _mirror.SyncDelayMs;
            SyncDelayLabel.Text = $"{_mirror.SyncDelayMs} ms";
        }
        MultiOutputCard.BorderBrush = mirroring
            ? new SolidColorBrush(Color.FromRgb(0x06, 0xB6, 0xD4))
            : new SolidColorBrush(Color.FromArgb(0x34, 0xFF, 0xFF, 0xFF));
    }

    private void LoadMicrophones(IReadOnlyList<AudioEndpointInfo> all)
    {
        _auxMicEndpoint = all
            .Where(e => e.Category == AudioCategory.AuxWired)
            .OrderByDescending(e => e.IsActive)
            .ThenByDescending(e => e.IsDefaultPlayback)
            .FirstOrDefault();

        _builtInMicEndpoint = all
            .Where(e => e.Category == AudioCategory.BuiltInMic)
            .OrderByDescending(e => e.IsActive)
            .ThenByDescending(e => e.IsDefaultPlayback)
            .FirstOrDefault();

        _btMicEndpoints = all
            .Where(e => e.Category == AudioCategory.Bluetooth)
            .OrderByDescending(e => e.IsActive)
            .ThenByDescending(e => e.IsDefaultPlayback)
            .ToList();

        if (!string.IsNullOrEmpty(_preferredBtMicId))
        {
            _selectedBtMicEndpoint = _btMicEndpoints.FirstOrDefault(e => e.Id == _preferredBtMicId);
        }
        _selectedBtMicEndpoint ??= _btMicEndpoints.FirstOrDefault();
        if (_selectedBtMicEndpoint != null)
        {
            _preferredBtMicId = _selectedBtMicEndpoint.Id;
        }

        _otherMicEndpoints = all
            .Where(e => e.Category == AudioCategory.Other && !e.IsDisabled)
            .OrderByDescending(e => e.IsActive)
            .ThenByDescending(e => e.IsDefaultPlayback)
            .ToList();

        if (!string.IsNullOrEmpty(_preferredOtherMicId))
        {
            _selectedOtherMicEndpoint = _otherMicEndpoints.FirstOrDefault(e => e.Id == _preferredOtherMicId);
        }
        _selectedOtherMicEndpoint ??= _otherMicEndpoints.FirstOrDefault();
        if (_selectedOtherMicEndpoint != null)
        {
            _preferredOtherMicId = _selectedOtherMicEndpoint.Id;
        }

        UpdateAuxMicCard();
        UpdateBtMicCard();
        UpdateBuiltInMicCard();
        UpdateOtherMicCard();

        var activeMic = all.FirstOrDefault(e => e.IsDefaultPlayback);
        ActiveMicHeaderLabel.Text = activeMic != null
            ? $"Active Mic • {activeMic.FriendlyName}"
            : "No active microphone";
    }

    private void UpdateHeaderAndPill(IReadOnlyList<AudioEndpointInfo> renders, IReadOnlyList<AudioEndpointInfo> captures)
    {
        var outEp = renders.FirstOrDefault(e => e.IsDefaultPlayback);
        var micEp = captures.FirstOrDefault(e => e.IsDefaultPlayback);

        string outLabel = outEp == null ? "No Out" : outEp.Category switch
        {
            AudioCategory.Bluetooth => outEp.ShortName,
            AudioCategory.AuxWired => "AUX",
            AudioCategory.BuiltInSpeakers => "Speakers",
            _ => outEp.ShortName
        };

        string micLabel = micEp == null ? "No Mic" : micEp.Category switch
        {
            AudioCategory.Bluetooth => "BT Mic",
            AudioCategory.AuxWired => "AUX Mic",
            AudioCategory.BuiltInMic => "Laptop Mic",
            _ => micEp.ShortName
        };

        HeaderSubtitle.Text = _mirror.IsRunning
            ? $"Mirroring {_multiSelectedIds.Count} outputs  •  {micLabel}"
            : $"{outLabel}  •  {micLabel}";
        _lastRouteLabel = _mirror.IsRunning
            ? $"Mirror ({_multiSelectedIds.Count})"
            : (outEp?.IsMuted == true ? $"{outLabel} (Muted)" : outLabel);
        _lastRouteIcon = _mirror.IsRunning
            ? "🔀"
            : outEp?.Category switch
            {
                AudioCategory.Bluetooth => "ᛒ",
                AudioCategory.AuxWired => "🎧",
                AudioCategory.BuiltInSpeakers => "🔈",
                _ => "🔊"
            };
        PillMicBadge.Text = micEp?.IsMuted == true ? $"{micLabel} • Muted" : micLabel;
        MediaOutputRouteText.Text = _lastRouteLabel;

        Color dotColor = _mirror.IsRunning
            ? Color.FromRgb(0x06, 0xB6, 0xD4)
            : outEp?.Category switch
            {
                AudioCategory.Bluetooth => Color.FromRgb(0x3B, 0x82, 0xF6),
                AudioCategory.AuxWired => Color.FromRgb(0x10, 0xB9, 0x81),
                AudioCategory.BuiltInSpeakers => Color.FromRgb(0xF5, 0x9E, 0x0B),
                _ => Color.FromRgb(0x64, 0x74, 0x8B)
            };
        var brush = new SolidColorBrush(dotColor);
        _currentAudioAccent = dotColor;
        PillBeaconDot.Background = brush;
        ThinLineAccentStop.Color = dotColor;

        string iconKey = _mirror.IsRunning
            ? "IconMirror"
            : outEp?.Category switch
            {
                AudioCategory.Bluetooth => "IconBluetooth",
                AudioCategory.BuiltInSpeakers => "IconSpeaker",
                _ => "IconHeadphones"
            };
        if (System.Windows.Application.Current.TryFindResource(iconKey) is Geometry geo)
        {
            PillAudioIconPath.Data = geo;
            PillAudioIconPath.Fill = brush;
        }
        PillAudioBadge.Background = new SolidColorBrush(Color.FromArgb(0x30, dotColor.R, dotColor.G, dotColor.B));
        PillAudioBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, dotColor.R, dotColor.G, dotColor.B));

        UpdateCompactPillMediaAndRoute();
        UpdateHeaderForPage();

        if (_trayIcon != null)
        {
            string tip = _mirror.IsRunning
                ? $"Audio Switch • Mirroring {_multiSelectedIds.Count} | {micLabel}"
                : $"Audio Switch • {outLabel} | {micLabel}";
            _trayIcon.Text = tip.Length > 63 ? tip[..63] : tip;
        }
    }

    private void UpdateAuxCard()
    {
        if (_auxEndpoint == null)
        {
            AuxDeviceName.Text = "No AUX / Wired headphones found";
            AuxStatusText.Text = "Plug in a 3.5mm cable";
            AuxToggle.IsChecked = false;
            AuxToggle.IsEnabled = false;
            AuxMuteToggle.IsChecked = false;
            AuxMuteToggle.IsEnabled = false;
            AuxBadge.Visibility = Visibility.Collapsed;
            AuxVolumeRow.Visibility = Visibility.Collapsed;
            AuxMeterTrack.Visibility = Visibility.Collapsed;
            AuxCard.BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            return;
        }

        bool isOn = _auxEndpoint.IsActive && _auxEndpoint.IsDefaultPlayback && !_auxEndpoint.IsMuted;
        AuxToggle.IsEnabled = true;
        AuxToggle.IsChecked = isOn;
        AuxMuteToggle.IsEnabled = _auxEndpoint.IsActive;
        AuxMuteToggle.IsChecked = _auxEndpoint.IsActive && _auxEndpoint.IsMuted;
        AuxDeviceName.Text = _auxEndpoint.FriendlyName;
        AuxStatusText.Text = _auxEndpoint.StateLabel;

        AuxBadge.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
        AuxCard.BorderBrush = isOn
            ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81))
            : new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));

        AuxVolumeRow.Visibility = _auxEndpoint.IsActive && (_auxEndpoint.IsDefaultPlayback || _mirror.IsRunning)
            ? Visibility.Visible
            : Visibility.Collapsed;
        AuxMeterTrack.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
        if (_auxEndpoint.IsActive)
        {
            AuxVolumeSlider.Value = _auxEndpoint.VolumePercent;
            AuxVolumeLabel.Text = $"{_auxEndpoint.VolumePercent}%";
        }
    }

    private void UpdateBluetoothCard()
    {
        if (_btEndpoints.Count == 0 || _selectedBtEndpoint == null)
        {
            BtDeviceName.Visibility = Visibility.Visible;
            BtDeviceSelector.Visibility = Visibility.Collapsed;
            BtDeviceName.Text = "No Bluetooth audio devices paired";
            BtStatusText.Text = "Pair a Bluetooth headset in Windows";
            BtToggle.IsChecked = false;
            BtToggle.IsEnabled = true;
            BtMuteToggle.IsChecked = false;
            BtMuteToggle.IsEnabled = false;
            BtBadge.Visibility = Visibility.Collapsed;
            BtVolumeRow.Visibility = Visibility.Collapsed;
            BtMeterTrack.Visibility = Visibility.Collapsed;
            BtConnectSettingsBtn.Visibility = Visibility.Visible;
            BtCard.BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            return;
        }

        if (_btEndpoints.Count > 1)
        {
            BtDeviceSelector.Visibility = Visibility.Visible;
            BtDeviceName.Visibility = Visibility.Collapsed;
            BtDeviceSelector.ItemsSource = _btEndpoints.Select(e =>
                $"{e.FriendlyName}{(e.IsActive ? " • Connected" : " • Disconnected")}").ToList();
            int idx = _btEndpoints.FindIndex(e => e.Id == _selectedBtEndpoint.Id);
            BtDeviceSelector.SelectedIndex = Math.Max(0, idx);
        }
        else
        {
            BtDeviceSelector.Visibility = Visibility.Collapsed;
            BtDeviceName.Visibility = Visibility.Visible;
            BtDeviceName.Text = _selectedBtEndpoint.FriendlyName;
        }

        bool isOn = _selectedBtEndpoint.IsActive && _selectedBtEndpoint.IsDefaultPlayback && !_selectedBtEndpoint.IsMuted;
        BtToggle.IsEnabled = true;
        BtToggle.IsChecked = isOn;
        BtMuteToggle.IsEnabled = _selectedBtEndpoint.IsActive;
        BtMuteToggle.IsChecked = _selectedBtEndpoint.IsActive && _selectedBtEndpoint.IsMuted;
        BtStatusText.Text = _selectedBtEndpoint.StateLabel;

        BtBadge.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
        BtCard.BorderBrush = isOn
            ? new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6))
            : new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));

        BtVolumeRow.Visibility = _selectedBtEndpoint.IsActive && (_selectedBtEndpoint.IsDefaultPlayback || _mirror.IsRunning)
            ? Visibility.Visible
            : Visibility.Collapsed;
        BtMeterTrack.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
        BtConnectSettingsBtn.Visibility = _selectedBtEndpoint.IsActive ? Visibility.Collapsed : Visibility.Visible;
        BtConnectSettingsBtn.Content = $"Connect {_selectedBtEndpoint.ShortName}";

        if (_selectedBtEndpoint.IsActive)
        {
            BtVolumeSlider.Value = _selectedBtEndpoint.VolumePercent;
            BtVolumeLabel.Text = $"{_selectedBtEndpoint.VolumePercent}%";
        }
    }

    private void UpdateSpeakersCard()
    {
        if (_speakersEndpoint == null || !_settings.ShowSpeakersCard)
        {
            SpeakersCard.Visibility = Visibility.Collapsed;
            return;
        }

        SpeakersCard.Visibility = Visibility.Visible;
        bool isOn = _speakersEndpoint.IsActive && _speakersEndpoint.IsDefaultPlayback && !_speakersEndpoint.IsMuted;
        SpeakersToggle.IsChecked = isOn;
        SpeakersToggle.IsEnabled = _speakersEndpoint.IsActive || _speakersEndpoint.IsDisabled;
        SpeakersMuteToggle.IsEnabled = _speakersEndpoint.IsActive;
        SpeakersMuteToggle.IsChecked = _speakersEndpoint.IsActive && _speakersEndpoint.IsMuted;
        SpeakersName.Text = _speakersEndpoint.FriendlyName;
        SpeakersStatus.Text = _speakersEndpoint.StateLabel;
        SpeakersCard.BorderBrush = isOn
            ? new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B))
            : new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));

        SpeakersVolumeRow.Visibility = _speakersEndpoint.IsActive && (_speakersEndpoint.IsDefaultPlayback || _mirror.IsRunning)
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (_speakersEndpoint.IsActive)
        {
            SpeakersVolumeSlider.Value = _speakersEndpoint.VolumePercent;
            SpeakersVolumeLabel.Text = $"{_speakersEndpoint.VolumePercent}%";
        }
    }

    private void UpdateAuxMicCard()
    {
        // Hide phantom/subwoofer AUX jack mic unless user explicitly opts in or it is currently default
        bool shouldShow = _auxMicEndpoint != null &&
                          (ShowAuxMicCheck.IsChecked == true || _auxMicEndpoint.IsDefaultPlayback);
        if (!shouldShow || _auxMicEndpoint == null)
        {
            AuxMicCard.Visibility = Visibility.Collapsed;
            return;
        }

        AuxMicCard.Visibility = Visibility.Visible;
        bool isOn = _auxMicEndpoint.IsActive && _auxMicEndpoint.IsDefaultPlayback && !_auxMicEndpoint.IsMuted;
        AuxMicToggle.IsEnabled = _auxMicEndpoint.IsActive || _auxMicEndpoint.IsDisabled;
        AuxMicToggle.IsChecked = isOn;
        AuxMicMuteToggle.IsEnabled = _auxMicEndpoint.IsActive;
        AuxMicMuteToggle.IsChecked = _auxMicEndpoint.IsActive && _auxMicEndpoint.IsMuted;
        AuxMicName.Text = _auxMicEndpoint.FriendlyName;
        AuxMicStatus.Text = _auxMicEndpoint.StateLabel;
        AuxMicBadge.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
        AuxMicCard.BorderBrush = isOn
            ? new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7))
            : new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));

        AuxMicVolumeRow.Visibility = _auxMicEndpoint.IsActive && _auxMicEndpoint.IsDefaultPlayback
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (_auxMicEndpoint.IsActive)
        {
            AuxMicSlider.Value = _auxMicEndpoint.VolumePercent;
            AuxMicVolLabel.Text = $"{_auxMicEndpoint.VolumePercent}%";
        }
    }

    private void UpdateBtMicCard()
    {
        if (_btMicEndpoints.Count == 0 || _selectedBtMicEndpoint == null)
        {
            BtMicCard.Visibility = Visibility.Collapsed;
            return;
        }

        BtMicCard.Visibility = Visibility.Visible;
        if (_btMicEndpoints.Count > 1)
        {
            BtMicSelector.Visibility = Visibility.Visible;
            BtMicName.Visibility = Visibility.Collapsed;
            BtMicSelector.ItemsSource = _btMicEndpoints.Select(e =>
                $"{e.FriendlyName}{(e.IsActive ? " • Connected" : " • Disconnected")}").ToList();
            int idx = _btMicEndpoints.FindIndex(e => e.Id == _selectedBtMicEndpoint.Id);
            BtMicSelector.SelectedIndex = Math.Max(0, idx);
        }
        else
        {
            BtMicSelector.Visibility = Visibility.Collapsed;
            BtMicName.Visibility = Visibility.Visible;
            BtMicName.Text = _selectedBtMicEndpoint.FriendlyName;
        }

        bool isOn = _selectedBtMicEndpoint.IsActive && _selectedBtMicEndpoint.IsDefaultPlayback && !_selectedBtMicEndpoint.IsMuted;
        BtMicToggle.IsEnabled = _selectedBtMicEndpoint.IsActive || _selectedBtMicEndpoint.IsDisabled;
        BtMicToggle.IsChecked = isOn;
        BtMicMuteToggle.IsEnabled = _selectedBtMicEndpoint.IsActive;
        BtMicMuteToggle.IsChecked = _selectedBtMicEndpoint.IsActive && _selectedBtMicEndpoint.IsMuted;
        BtMicStatus.Text = _selectedBtMicEndpoint.IsActive
            ? _selectedBtMicEndpoint.StateLabel
            : "Connect Bluetooth headset first";
        BtMicBadge.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
        BtMicCard.BorderBrush = isOn
            ? new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC))
            : new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));

        BtMicVolumeRow.Visibility = _selectedBtMicEndpoint.IsActive && _selectedBtMicEndpoint.IsDefaultPlayback
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (_selectedBtMicEndpoint.IsActive)
        {
            BtMicSlider.Value = _selectedBtMicEndpoint.VolumePercent;
            BtMicVolLabel.Text = $"{_selectedBtMicEndpoint.VolumePercent}%";
        }
    }

    private void UpdateBuiltInMicCard()
    {
        if (_builtInMicEndpoint == null)
        {
            BuiltInMicCard.Visibility = Visibility.Collapsed;
            return;
        }

        BuiltInMicCard.Visibility = Visibility.Visible;
        bool isOn = _builtInMicEndpoint.IsActive && _builtInMicEndpoint.IsDefaultPlayback && !_builtInMicEndpoint.IsMuted;
        BuiltInMicToggle.IsChecked = isOn;
        BuiltInMicToggle.IsEnabled = _builtInMicEndpoint.IsActive || _builtInMicEndpoint.IsDisabled;
        BuiltInMicMuteToggle.IsEnabled = _builtInMicEndpoint.IsActive;
        BuiltInMicMuteToggle.IsChecked = _builtInMicEndpoint.IsActive && _builtInMicEndpoint.IsMuted;
        BuiltInMicName.Text = _builtInMicEndpoint.FriendlyName;
        BuiltInMicStatus.Text = _builtInMicEndpoint.StateLabel;
        BuiltInMicCard.BorderBrush = isOn
            ? new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7))
            : new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));

        BuiltInMicVolumeRow.Visibility = _builtInMicEndpoint.IsActive && _builtInMicEndpoint.IsDefaultPlayback
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (_builtInMicEndpoint.IsActive)
        {
            BuiltInMicSlider.Value = _builtInMicEndpoint.VolumePercent;
            BuiltInMicVolLabel.Text = $"{_builtInMicEndpoint.VolumePercent}%";
        }
    }

    private void UpdateOtherMicCard()
    {
        if (_otherMicEndpoints.Count == 0 || _selectedOtherMicEndpoint == null)
        {
            OtherMicCard.Visibility = Visibility.Collapsed;
            return;
        }

        OtherMicCard.Visibility = Visibility.Visible;
        OtherMicSelector.ItemsSource = _otherMicEndpoints.Select(e => e.FriendlyName).ToList();
        int idx = _otherMicEndpoints.FindIndex(e => e.Id == _selectedOtherMicEndpoint.Id);
        OtherMicSelector.SelectedIndex = Math.Max(0, idx);

        bool isOn = _selectedOtherMicEndpoint.IsActive && _selectedOtherMicEndpoint.IsDefaultPlayback && !_selectedOtherMicEndpoint.IsMuted;
        OtherMicToggle.IsChecked = isOn;
        OtherMicToggle.IsEnabled = _selectedOtherMicEndpoint.IsActive;
        OtherMicMuteToggle.IsEnabled = _selectedOtherMicEndpoint.IsActive;
        OtherMicMuteToggle.IsChecked = _selectedOtherMicEndpoint.IsActive && _selectedOtherMicEndpoint.IsMuted;
        OtherMicCard.BorderBrush = isOn
            ? new SolidColorBrush(Color.FromRgb(0xEC, 0x48, 0x99))
            : new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
    }

    // --- Tab Switching Animations ---
    private void OutputTabBtn_Click(object sender, RoutedEventArgs e) => SwitchTab(toMic: false);
    private void MicTabBtn_Click(object sender, RoutedEventArgs e) => SwitchTab(toMic: true);

    private void SwitchTab(bool toMic)
    {
        if (_isMicTabActive == toMic) return;
        _isMicTabActive = toMic;

        double targetX = toMic ? TabIndicator.ActualWidth : 0;
        var quart = new QuarticEase { EasingMode = EasingMode.EaseOut };
        TabIndicatorSlide.BeginAnimation(TranslateTransform.XProperty, SmoothAnim(targetX, 165, quart));

        OutputTabBtn.Foreground = new SolidColorBrush(toMic ? Color.FromRgb(0x94, 0xA3, 0xB8) : Colors.White);
        MicTabBtn.Foreground = new SolidColorBrush(toMic ? Colors.White : Color.FromRgb(0x94, 0xA3, 0xB8));

        if (toMic)
        {
            OutputsPanel.Visibility = Visibility.Collapsed;
            MicsPanel.Visibility = Visibility.Visible;
            AnimatePanelEnter(MicsPanel, MicsPanelSlide, fromX: 20);
        }
        else
        {
            MicsPanel.Visibility = Visibility.Collapsed;
            OutputsPanel.Visibility = Visibility.Visible;
            AnimatePanelEnter(OutputsPanel, OutputsPanelSlide, fromX: -20);
        }
        SpringExpandedHeightToFit();
    }

    private static void AnimatePanelEnter(UIElement panel, TranslateTransform slide, double fromX)
    {
        var quart = new QuarticEase { EasingMode = EasingMode.EaseOut };
        panel.BeginAnimation(OpacityProperty, SmoothAnim(0.25, 1.0, 150, quart));
        slide.BeginAnimation(TranslateTransform.XProperty, SmoothAnim(fromX, 0, 155, quart));
    }

    // --- Output Actions ---
    private async void AuxToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _auxEndpoint == null) return;
        if (AuxToggle.IsChecked == true) await ActivateAuxAsync();
        else await TurnOffAuxAndRouteFallbackAsync();
    }

    private async void BtToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;
        if (BtToggle.IsChecked == true) await ActivateBluetoothAsync();
        else await TurnOffBluetoothAndRouteFallbackAsync();
    }

    private void SpeakersToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _speakersEndpoint == null) return;
        if (SpeakersToggle.IsChecked == true)
        {
            ActivateSpeakers();
            return;
        }

        var bt = _btEndpoints.FirstOrDefault(x => x.IsActive);
        if (bt != null) _ = ActivateBluetoothAsync();
        else if (_auxEndpoint is { IsActive: true }) _ = ActivateAuxAsync();
        else
        {
            _router.SetMute(_speakersEndpoint.Id, true);
            SetStatus("Laptop speakers muted.");
            RefreshDevices();
        }
    }

    private Task ActivateAuxAsync()
    {
        if (_auxEndpoint == null) return Task.CompletedTask;
        _mirror.StopMirroring();

        if (_auxEndpoint.IsDisabled)
        {
            _router.SetEndpointVisibility(_auxEndpoint.Id, true);
            RefreshDevices();
        }

        if (!_auxEndpoint.IsActive)
        {
            SetStatus("AUX jack is unplugged or unavailable.");
            RefreshDevices();
            return Task.CompletedTask;
        }

        var res = _router.SwitchToEndpoint(_auxEndpoint.Id, GetOutputMuteTargetsExcluding(_auxEndpoint.Id), unmuteTarget: true);
        SetStatus(res.Success ? $"Switched output to AUX ({_auxEndpoint.FriendlyName})." : res.Message);
        RefreshDevices();
        return Task.CompletedTask;
    }

    private async Task TurnOffAuxAndRouteFallbackAsync()
    {
        if (_auxEndpoint == null) return;
        _mirror.StopMirroring();

        var connectedBt = (_selectedBtEndpoint?.IsActive == true ? _selectedBtEndpoint : null)
                          ?? _btEndpoints.FirstOrDefault(b => b.IsActive && !b.IsHandsFree)
                          ?? _btEndpoints.FirstOrDefault(b => b.IsActive);

        AudioEndpointInfo? fallback = connectedBt ?? (_speakersEndpoint?.IsActive == true ? _speakersEndpoint : null);

        if (fallback == null && _selectedBtEndpoint is { IsUnplugged: true })
        {
            SetStatus($"AUX off requested — connecting {_selectedBtEndpoint.ShortName}...");
            var btConn = await _router.TryConnectBluetoothAudioAsync(_selectedBtEndpoint.ShortName);
            RefreshDevices();
            if (btConn.Success) fallback = _btEndpoints.FirstOrDefault(b => b.IsActive);
            else SetStatus(btConn.Message);
        }

        if (fallback != null)
        {
            var res = _router.SwitchToEndpoint(fallback.Id, new[] { _auxEndpoint.Id }, unmuteTarget: true);
            if (HardDisableAuxCheck.IsChecked == true)
            {
                var vis = _router.SetEndpointVisibility(_auxEndpoint.Id, false);
                SetStatus(vis.Success
                    ? $"AUX disabled in Windows • Playing on {fallback.FriendlyName}"
                    : $"{vis.Message} Playing on {fallback.FriendlyName}.");
            }
            else
            {
                SetStatus(res.Success ? $"AUX turned off (muted) • Playing on {fallback.FriendlyName}" : res.Message);
            }
        }
        else
        {
            _router.SetMute(_auxEndpoint.Id, true);
            if (HardDisableAuxCheck.IsChecked == true) _router.SetEndpointVisibility(_auxEndpoint.Id, false);
            SetStatus("AUX turned off (muted). No connected Bluetooth fallback found.");
        }

        RefreshDevices();
    }

    private async Task ActivateBluetoothAsync()
    {
        _mirror.StopMirroring();
        if (_selectedBtEndpoint == null)
        {
            SetStatus("No Bluetooth headset paired. Opening Windows Bluetooth settings...");
            OpenBluetoothSettings();
            RefreshDevices();
            return;
        }

        if (_selectedBtEndpoint.IsDisabled)
        {
            _router.SetEndpointVisibility(_selectedBtEndpoint.Id, true);
            RefreshDevices();
        }

        if (!_selectedBtEndpoint.IsActive)
        {
            SetStatus($"Connecting to {_selectedBtEndpoint.ShortName}...");
            BtToggle.IsEnabled = false;
            var conn = await _router.TryConnectBluetoothAudioAsync(_selectedBtEndpoint.ShortName);
            BtToggle.IsEnabled = true;

            await Task.Delay(500);
            RefreshDevices();

            if (!conn.Success && (_selectedBtEndpoint == null || !_selectedBtEndpoint.IsActive))
            {
                SetStatus(conn.Message);
                return;
            }
        }

        var target = _btEndpoints.FirstOrDefault(b => b.Id == _selectedBtEndpoint?.Id && b.IsActive)
                     ?? _btEndpoints.FirstOrDefault(b => b.IsActive && !b.IsHandsFree)
                     ?? _btEndpoints.FirstOrDefault(b => b.IsActive);

        if (target == null)
        {
            SetStatus("Bluetooth headset is not connected yet. Turn on the headset and click Connect.");
            RefreshDevices();
            return;
        }

        var res = _router.SwitchToEndpoint(target.Id, GetOutputMuteTargetsExcluding(target.Id), unmuteTarget: true);

        if (res.Success && HardDisableAuxCheck.IsChecked == true && _auxEndpoint is { IsActive: true })
        {
            var vis = _router.SetEndpointVisibility(_auxEndpoint.Id, false);
            SetStatus(vis.Success
                ? $"Bluetooth ON ({target.FriendlyName}) • AUX disabled"
                : $"Bluetooth ON ({target.FriendlyName}) • AUX muted");
        }
        else
        {
            SetStatus(res.Success ? $"Bluetooth ON ({target.FriendlyName}) • AUX off" : res.Message);
        }

        RefreshDevices();
    }

    private Task TurnOffBluetoothAndRouteFallbackAsync()
    {
        if (_selectedBtEndpoint == null) return Task.CompletedTask;
        _mirror.StopMirroring();

        if (_auxEndpoint != null && (_auxEndpoint.IsActive || _auxEndpoint.IsDisabled))
        {
            if (_auxEndpoint.IsDisabled)
            {
                _router.SetEndpointVisibility(_auxEndpoint.Id, true);
                RefreshDevices();
            }

            if (_auxEndpoint.IsActive)
            {
                _router.SwitchToEndpoint(_auxEndpoint.Id, new[] { _selectedBtEndpoint.Id }, unmuteTarget: true);
                SetStatus($"Bluetooth muted • Switched to AUX ({_auxEndpoint.FriendlyName})");
                RefreshDevices();
                return Task.CompletedTask;
            }
        }

        if (_speakersEndpoint is { IsActive: true })
        {
            _router.SwitchToEndpoint(_speakersEndpoint.Id, new[] { _selectedBtEndpoint.Id }, unmuteTarget: true);
            SetStatus($"Bluetooth muted • Switched to {_speakersEndpoint.FriendlyName}");
        }
        else
        {
            _router.SetMute(_selectedBtEndpoint.Id, true);
            SetStatus("Bluetooth muted.");
        }

        RefreshDevices();
        return Task.CompletedTask;
    }

    private void ActivateSpeakers()
    {
        if (_speakersEndpoint == null) return;
        _mirror.StopMirroring();
        if (_speakersEndpoint.IsDisabled)
        {
            _router.SetEndpointVisibility(_speakersEndpoint.Id, true);
            RefreshDevices();
        }

        var res = _router.SwitchToEndpoint(_speakersEndpoint.Id, GetOutputMuteTargetsExcluding(_speakersEndpoint.Id), unmuteTarget: true);
        SetStatus(res.Success ? $"Switched output to {_speakersEndpoint.FriendlyName}." : res.Message);
        RefreshDevices();
    }

    private IEnumerable<string> GetOutputMuteTargetsExcluding(string activeId)
    {
        if (AutoMuteCheck.IsChecked != true) return Array.Empty<string>();

        var ids = new List<string>();
        if (_auxEndpoint is { IsActive: true } && _auxEndpoint.Id != activeId) ids.Add(_auxEndpoint.Id);
        if (_speakersEndpoint is { IsActive: true } && _speakersEndpoint.Id != activeId) ids.Add(_speakersEndpoint.Id);
        foreach (var bt in _btEndpoints.Where(b => b.IsActive && b.Id != activeId)) ids.Add(bt.Id);
        return ids;
    }

    private void MultiDeviceCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;

        var checkedOptions = _multiOptions.Where(o => o.IsCheckable && o.IsChecked).ToList();
        _multiSelectedIds.Clear();
        foreach (var opt in checkedOptions)
        {
            _multiSelectedIds.Add(opt.Id);
        }

        if (checkedOptions.Count == 0)
        {
            _mirror.StopMirroring();
            SetStatus("Select at least 1 output device.");
            RefreshDevices();
            return;
        }

        if (checkedOptions.Count == 1)
        {
            _mirror.StopMirroring();
            var single = checkedOptions[0];
            _router.SwitchToEndpoint(single.Id, GetOutputMuteTargetsExcluding(single.Id), unmuteTarget: true);
            SetStatus($"Single output active: {single.ShortLabel}");
            RefreshDevices();
            return;
        }

        // Prioritize Bluetooth as Primary when mirroring BT + Wired so we can delay wired output to match BT latency
        var allRenders = _router.GetRenderEndpoints();
        var btChecked = checkedOptions.FirstOrDefault(o => o.Category == AudioCategory.Bluetooth);
        var currentDefault = allRenders.FirstOrDefault(r => r.IsDefaultPlayback && r.IsActive && _multiSelectedIds.Contains(r.Id));
        string primaryId = btChecked?.Id ?? currentDefault?.Id ?? checkedOptions[0].Id;
        bool primaryIsBt = btChecked != null;

        var secondaryOptions = checkedOptions
            .Where(o => !string.Equals(o.Id, primaryId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var secondaryIds = secondaryOptions.Select(o => o.Id).ToList();
        bool hasWiredSecondary = secondaryOptions.Any(o => o.Category is AudioCategory.AuxWired or AudioCategory.BuiltInSpeakers);

        _mirrorHasBtAndWired = primaryIsBt && hasWiredSecondary;
        int initialDelayMs = _mirrorHasBtAndWired ? _btWiredDelayMs : _wiredDelayMs;

        // Ensure primary is default & unmuted, secondaries are unmuted, and unchecked outputs are muted
        _router.SwitchToEndpoint(primaryId, Array.Empty<string>(), unmuteTarget: true);
        foreach (var secId in secondaryIds)
        {
            _router.SetMute(secId, false);
        }
        if (AutoMuteCheck.IsChecked == true)
        {
            foreach (var opt in _multiOptions.Where(o => o.IsCheckable && !_multiSelectedIds.Contains(o.Id)))
            {
                _router.SetMute(opt.Id, true);
            }
        }

        _mirror.StartMirroring(primaryId, secondaryIds, initialDelayMs);
        string names = string.Join(" + ", checkedOptions.Select(o => o.ShortLabel));
        SetStatus($"Mirroring ({names}) • Sync delay {initialDelayMs} ms (slide to fine-tune)");
        RefreshDevices();
    }

    private void StopMultiBtn_Click(object sender, RoutedEventArgs e)
    {
        _mirror.StopMirroring();
        var allRenders = _router.GetRenderEndpoints();
        var primary = allRenders.FirstOrDefault(r => r.IsDefaultPlayback && r.IsActive);
        if (primary != null)
        {
            _router.SwitchToEndpoint(primary.Id, GetOutputMuteTargetsExcluding(primary.Id), unmuteTarget: true);
            SetStatus($"Returned to single output: {primary.FriendlyName}");
        }
        RefreshDevices();
    }

    // --- Microphone Actions ---
    private void AuxMicToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _auxMicEndpoint == null) return;
        if (AuxMicToggle.IsChecked == true) ActivateMic(_auxMicEndpoint, "AUX Mic");
        else TurnOffMicAndFallback(_auxMicEndpoint);
    }

    private void BtMicToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _selectedBtMicEndpoint == null) return;
        if (BtMicToggle.IsChecked == true) ActivateMic(_selectedBtMicEndpoint, "Bluetooth Mic");
        else TurnOffMicAndFallback(_selectedBtMicEndpoint);
    }

    private void BuiltInMicToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _builtInMicEndpoint == null) return;
        if (BuiltInMicToggle.IsChecked == true) ActivateMic(_builtInMicEndpoint, "Laptop Mic");
        else TurnOffMicAndFallback(_builtInMicEndpoint);
    }

    private void OtherMicToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || _selectedOtherMicEndpoint == null) return;
        if (OtherMicToggle.IsChecked == true) ActivateMic(_selectedOtherMicEndpoint, _selectedOtherMicEndpoint.FriendlyName);
        else TurnOffMicAndFallback(_selectedOtherMicEndpoint);
    }

    private void ActivateMic(AudioEndpointInfo target, string label)
    {
        if (target.IsDisabled)
        {
            _router.SetEndpointVisibility(target.Id, true);
            RefreshDevices();
        }

        if (!target.IsActive)
        {
            SetStatus($"{label} is disconnected or unavailable.");
            RefreshDevices();
            return;
        }

        var res = _router.SwitchToEndpoint(target.Id, GetMicMuteTargetsExcluding(target.Id), unmuteTarget: true);
        SetStatus(res.Success ? $"Mic switched to {target.FriendlyName}." : res.Message);
        RefreshDevices();
    }

    private void TurnOffMicAndFallback(AudioEndpointInfo turnedOff)
    {
        AudioEndpointInfo? fallback = new[]
        {
            _builtInMicEndpoint,
            _auxMicEndpoint,
            _btMicEndpoints.FirstOrDefault(m => m.IsActive),
            _otherMicEndpoints.FirstOrDefault(m => m.IsActive)
        }.FirstOrDefault(m => m != null && m.IsActive && m.Id != turnedOff.Id);

        if (fallback != null)
        {
            _router.SwitchToEndpoint(fallback.Id, new[] { turnedOff.Id }, unmuteTarget: true);
            SetStatus($"Muted {turnedOff.ShortName} • Switched mic to {fallback.FriendlyName}");
        }
        else
        {
            _router.SetMute(turnedOff.Id, true);
            SetStatus($"Muted {turnedOff.FriendlyName}.");
        }

        RefreshDevices();
    }

    private IEnumerable<string> GetMicMuteTargetsExcluding(string activeId)
    {
        if (AutoMuteCheck.IsChecked != true) return Array.Empty<string>();

        var ids = new List<string>();
        if (_auxMicEndpoint is { IsActive: true } && _auxMicEndpoint.Id != activeId) ids.Add(_auxMicEndpoint.Id);
        if (_builtInMicEndpoint is { IsActive: true } && _builtInMicEndpoint.Id != activeId) ids.Add(_builtInMicEndpoint.Id);
        foreach (var m in _btMicEndpoints.Where(x => x.IsActive && x.Id != activeId)) ids.Add(m.Id);
        foreach (var m in _otherMicEndpoints.Where(x => x.IsActive && x.Id != activeId)) ids.Add(m.Id);
        return ids;
    }

    // --- Header & Drag Controls ---
    private async void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        bool auxIsCurrentlyOn = _auxEndpoint is { IsActive: true, IsDefaultPlayback: true, IsMuted: false };
        if (auxIsCurrentlyOn) await ActivateBluetoothAsync();
        else await ActivateAuxAsync();
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _isPinnedOpen = !_isPinnedOpen;
        PinButton.Foreground = _isPinnedOpen
            ? new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA))
            : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        SetStatus(_isPinnedOpen ? "Big box locked open." : "Auto-collapse to top line enabled.");
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        _isPinnedOpen = false;
        PinButton.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        CollapseToThinLine();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void IslandShell_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Let inner ScrollViewers (e.g. Multi-Output list) scroll normally
        if (e.OriginalSource is DependencyObject dep)
        {
            for (DependencyObject? cur = dep; cur != null; cur = VisualTreeHelper.GetParent(cur))
            {
                if (cur is ScrollViewer) return;
                if (cur == IslandShell) break;
            }
        }

        long now = Environment.TickCount64;
        if (now - _lastWheelSwipeTicks < 110) return;
        _lastWheelSwipeTicks = now;

        int dir = e.Delta < 0 ? 1 : -1;
        CycleIslandPage(dir);
        e.Handled = true;
    }

    private void PrevIslandBtn_Click(object sender, RoutedEventArgs e) => CycleIslandPage(-1);
    private void NextIslandBtn_Click(object sender, RoutedEventArgs e) => CycleIslandPage(1);
    private void NavAudioBtn_Click(object sender, RoutedEventArgs e) => SwitchIslandPage(IslandPage.Audio, 0);
    private void NavMediaBtn_Click(object sender, RoutedEventArgs e) => SwitchIslandPage(IslandPage.Media, 0);
    private void NavBatteryBtn_Click(object sender, RoutedEventArgs e) => SwitchIslandPage(IslandPage.Battery, 0);

    private List<IslandPage> GetEnabledPages()
    {
        var pages = new List<IslandPage> { IslandPage.Audio };
        if (_settings.ShowMediaPlayer) pages.Add(IslandPage.Media);
        if (_settings.ShowLaptopBattery) pages.Add(IslandPage.Battery);
        return pages;
    }

    private void CycleIslandPage(int delta)
    {
        var pages = GetEnabledPages();
        if (pages.Count <= 1) return;
        int idx = pages.IndexOf(_activePage);
        if (idx < 0) idx = 0;
        int nextIdx = (idx + delta + pages.Count) % pages.Count;
        SwitchIslandPage(pages[nextIdx], delta);
    }

    private void SwitchIslandPage(IslandPage target, int explicitDir)
    {
        var pages = GetEnabledPages();
        if (!pages.Contains(target)) target = pages[0];
        if (target == _activePage && explicitDir == 0)
        {
            UpdatePageIndicatorVisuals();
            return;
        }

        int dir = explicitDir != 0 ? explicitDir : ((int)target > (int)_activePage ? 1 : -1);
        _activePage = target;

        PillAudioPage.Visibility = target == IslandPage.Audio ? Visibility.Visible : Visibility.Collapsed;
        PillMediaPage.Visibility = target == IslandPage.Media ? Visibility.Visible : Visibility.Collapsed;
        PillBatteryPage.Visibility = target == IslandPage.Battery ? Visibility.Visible : Visibility.Collapsed;

        AudioIslandPage.Visibility = target == IslandPage.Audio ? Visibility.Visible : Visibility.Collapsed;
        MediaIslandPage.Visibility = target == IslandPage.Media ? Visibility.Visible : Visibility.Collapsed;
        BatteryIslandPage.Visibility = target == IslandPage.Battery ? Visibility.Visible : Visibility.Collapsed;

        UpdatePageIndicatorVisuals();
        UpdateHeaderForPage();

        var quart = new QuarticEase { EasingMode = EasingMode.EaseOut };
        double offset = dir >= 0 ? 28 : -28;

        if (_state == IslandState.CompactPill)
        {
            PillCarouselHost.BeginAnimation(OpacityProperty, SmoothAnim(0.25, 1.0, 150, quart));
            PillCarouselSlide.BeginAnimation(TranslateTransform.XProperty, SmoothAnim(offset, 0, 175, quart));
        }
        else if (_state == IslandState.ExpandedDeck && !_isCustomizing)
        {
            MainDeckHost.BeginAnimation(OpacityProperty, SmoothAnim(0.25, 1.0, 165, quart));
            DeckCarouselSlide.BeginAnimation(TranslateTransform.XProperty, SmoothAnim(offset * 1.25, 0, 190, quart));
            SpringExpandedHeightToFit();
        }

        if (target == IslandPage.Battery)
        {
            _ = RefreshTelemetryAsync();
        }
        else if (target == IslandPage.Media)
        {
            UpdateMediaProgressVisuals();
        }
        UpdateMediaProgressState();
    }

    private void SpringExpandedHeightToFit()
    {
        if (_state != IslandState.ExpandedDeck) return;
        ExpandedDeckView.Measure(new Size(ExpandedWidth - 2, double.PositiveInfinity));
        double targetHeight = Math.Clamp(ExpandedDeckView.DesiredSize.Height + 6, 170, 520);
        double curH = double.IsNaN(IslandShell.Height) ? IslandShell.ActualHeight : IslandShell.Height;
        if (curH <= 0) curH = targetHeight;
        _springH.Value = curH;
        IslandShell.Height = curH;
        _springH.Target = targetHeight;
        StartSpringLoop();
    }

    private void UpdatePageIndicatorVisuals()
    {
        var activeFill = Brushes.White;
        var idleFill = new SolidColorBrush(Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF));
        PillDot0.Fill = _activePage == IslandPage.Audio ? activeFill : idleFill;
        PillDot1.Fill = _activePage == IslandPage.Media ? activeFill : idleFill;
        PillDot2.Fill = _activePage == IslandPage.Battery ? activeFill : idleFill;

        var pages = GetEnabledPages();
        PillDot1.Visibility = pages.Contains(IslandPage.Media) ? Visibility.Visible : Visibility.Collapsed;
        PillDot2.Visibility = pages.Contains(IslandPage.Battery) ? Visibility.Visible : Visibility.Collapsed;

        NavMediaBtn.Visibility = pages.Contains(IslandPage.Media) ? Visibility.Visible : Visibility.Collapsed;
        NavBatteryBtn.Visibility = pages.Contains(IslandPage.Battery) ? Visibility.Visible : Visibility.Collapsed;

        int cols = pages.Count;
        if (IslandSwitcherStrip.Child is Grid navGrid && navGrid.ColumnDefinitions.Count == 3)
        {
            navGrid.ColumnDefinitions[1].Width = pages.Contains(IslandPage.Media) ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            navGrid.ColumnDefinitions[2].Width = pages.Contains(IslandPage.Battery) ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        }

        int visualSlot = Math.Max(0, pages.IndexOf(_activePage));
        double cellW = IslandNavIndicator.ActualWidth > 0
            ? IslandNavIndicator.ActualWidth
            : Math.Max(70, (ExpandedWidth - 32) / Math.Max(1, cols));
        IslandNavSlide.BeginAnimation(
            TranslateTransform.XProperty,
            SmoothAnim(visualSlot * cellW, 175, new QuarticEase { EasingMode = EasingMode.EaseOut }));

        var activeTxt = Brushes.White;
        var idleTxt = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        NavAudioBtn.Foreground = _activePage == IslandPage.Audio ? activeTxt : idleTxt;
        NavMediaBtn.Foreground = _activePage == IslandPage.Media ? activeTxt : idleTxt;
        NavBatteryBtn.Foreground = _activePage == IslandPage.Battery ? activeTxt : idleTxt;
    }

    private void UpdateHeaderForPage()
    {
        if (_isCustomizing)
        {
            IslandTitleText.Text = "Customize Island";
            ApplyKeylineTint(Color.FromRgb(0x60, 0xA5, 0xFA));
            return;
        }

        switch (_activePage)
        {
            case IslandPage.Audio:
                IslandTitleText.Text = "Audio Island";
                SetHeaderBadge("IconHeadphones", _currentAudioAccent);
                ApplyKeylineTint(_currentAudioAccent);
                break;
            case IslandPage.Media:
                bool isSpotify = !_lastMedia.HasSession || _lastMedia.SourceApp.Contains("Spotify", StringComparison.OrdinalIgnoreCase);
                Color mediaAccent = isSpotify ? Color.FromRgb(0x22, 0xC5, 0x5E) : Color.FromRgb(0x3B, 0x82, 0xF6);
                IslandTitleText.Text = _lastMedia.HasSession ? _lastMedia.SourceApp : "Spotify & Media";
                SetHeaderBadge("IconMusic", mediaAccent);
                ApplyKeylineTint(mediaAccent);
                break;
            case IslandPage.Battery:
                Color batAccent = Color.FromRgb(0x10, 0xB9, 0x81);
                IslandTitleText.Text = "Battery & System";
                SetHeaderBadge("IconBolt", batAccent);
                ApplyKeylineTint(batAccent);
                break;
        }
    }

    private void ApplyKeylineTint(Color accent)
    {
        AmbientGlowColorStop.Color = accent;
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x68, 0xFF, 0xFF, 0xFF), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x46, accent.R, accent.G, accent.B), 0.45));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x26, accent.R, accent.G, accent.B), 1));
        IslandShell.BorderBrush = brush;
    }

    private void SetHeaderBadge(string iconKey, Color accent)
    {
        if (System.Windows.Application.Current.TryFindResource(iconKey) is Geometry geo)
        {
            HeaderIconPath.Data = geo;
            HeaderIconPath.Fill = new SolidColorBrush(accent);
        }
        HeaderIconCircle.Background = new SolidColorBrush(Color.FromArgb(0x2E, accent.R, accent.G, accent.B));
        HeaderIconCircle.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, accent.R, accent.G, accent.B));
    }

    private void IslandShell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed) return;

        if (_state == IslandState.ThinLine)
        {
            if (e.ClickCount == 2)
            {
                PositionTopCenter();
                return;
            }
            ExpandToDeck();
            return;
        }

        if (_state == IslandState.CompactPill)
        {
            if (IsInteractiveControl(e.OriginalSource as DependencyObject, includeCheckBox: true)) return;
            _pillPressPoint = e.GetPosition(IslandShell);
            _pillPressTracking = true;
            _swipeTrackingDeck = false;
            _swipeDragMoved = false;
            IslandShell.CaptureMouse();
            return;
        }

        if (e.OriginalSource is DependencyObject dep)
        {
            // Dragging top header bar moves the floating Island across monitors
            if (IsDescendantOf(dep, ExpandedHeaderBar) && !IsInteractiveControl(dep, includeCheckBox: false))
            {
                _animateWindowPos = false;
                _isDragging = true;
                try { DragMove(); }
                catch { }
                finally { _isDragging = false; }
                return;
            }

            // Dragging anywhere inside the Expanded Island body (outside buttons/sliders/combos/checkboxes) slides between Islands
            if (!_isCustomizing && !IsInteractiveControl(dep, includeCheckBox: true))
            {
                _pillPressPoint = e.GetPosition(IslandShell);
                _pillPressTracking = true;
                _swipeTrackingDeck = true;
                _swipeDragMoved = false;
                IslandShell.CaptureMouse();
            }
        }
    }

    private void IslandShell_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_pillPressTracking || e.LeftButton != MouseButtonState.Pressed) return;

        var pt = e.GetPosition(IslandShell);
        double dx = pt.X - _pillPressPoint.X;
        double dy = pt.Y - _pillPressPoint.Y;

        if (!_swipeDragMoved)
        {
            if (Math.Abs(dx) < 6) return;
            if (_swipeTrackingDeck && Math.Abs(dx) < Math.Abs(dy) * 0.75) return;
            _swipeDragMoved = true;

            if (_swipeTrackingDeck)
            {
                DeckCarouselSlide.BeginAnimation(TranslateTransform.XProperty, null);
                MainDeckHost.BeginAnimation(OpacityProperty, null);
            }
            else
            {
                PillCarouselSlide.BeginAnimation(TranslateTransform.XProperty, null);
                PillCarouselHost.BeginAnimation(OpacityProperty, null);
            }
        }

        if (_swipeTrackingDeck)
        {
            DeckCarouselSlide.X = Math.Clamp(dx * 0.78, -92, 92);
            MainDeckHost.Opacity = Math.Max(0.38, 1.0 - Math.Abs(dx) / 145.0);
        }
        else
        {
            PillCarouselSlide.X = Math.Clamp(dx * 0.85, -72, 72);
            PillCarouselHost.Opacity = Math.Max(0.42, 1.0 - Math.Abs(dx) / 115.0);
        }
    }

    private void IslandShell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pillPressTracking) return;
        bool wasDeck = _swipeTrackingDeck;
        bool moved = _swipeDragMoved;
        _pillPressTracking = false;
        _swipeTrackingDeck = false;
        _swipeDragMoved = false;
        if (IslandShell.IsMouseCaptured)
        {
            IslandShell.ReleaseMouseCapture();
        }

        var releasePt = e.GetPosition(IslandShell);
        double dx = releasePt.X - _pillPressPoint.X;
        double threshold = wasDeck ? 28.0 : 20.0;

        if (Math.Abs(dx) >= threshold && GetEnabledPages().Count > 1)
        {
            CycleIslandPage(dx < 0 ? 1 : -1);
            return;
        }

        if (moved)
        {
            ResetCarouselSnapBack(wasDeck);
            return;
        }

        if (!wasDeck && _state == IslandState.CompactPill)
        {
            ExpandToDeck();
        }
    }

    private void IslandShell_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_pillPressTracking) return;
        bool wasDeck = _swipeTrackingDeck;
        _pillPressTracking = false;
        _swipeTrackingDeck = false;
        _swipeDragMoved = false;
        ResetCarouselSnapBack(wasDeck);
    }

    private void ResetCarouselSnapBack(bool deck)
    {
        var quart = new QuarticEase { EasingMode = EasingMode.EaseOut };
        if (deck)
        {
            MainDeckHost.BeginAnimation(OpacityProperty, SmoothAnim(1.0, 140, quart));
            DeckCarouselSlide.BeginAnimation(TranslateTransform.XProperty, SmoothAnim(0, 170, quart));
        }
        else
        {
            PillCarouselHost.BeginAnimation(OpacityProperty, SmoothAnim(1.0, 140, quart));
            PillCarouselSlide.BeginAnimation(TranslateTransform.XProperty, SmoothAnim(0, 170, quart));
        }
    }

    private static bool IsInteractiveControl(DependencyObject? leaf, bool includeCheckBox)
    {
        for (DependencyObject? cur = leaf; cur != null; cur = VisualTreeHelper.GetParent(cur))
        {
            if (cur is ButtonBase or Slider or ComboBox)
            {
                return true;
            }
            if (includeCheckBox && cur is CheckBox)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsDescendantOf(DependencyObject? leaf, DependencyObject ancestor)
    {
        for (DependencyObject? cur = leaf; cur != null; cur = VisualTreeHelper.GetParent(cur))
        {
            if (cur == ancestor) return true;
        }
        return false;
    }

    // --- Volume / Gain Sliders & Mutes ---
    private void AuxVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _auxEndpoint is not { IsActive: true }) return;
        int pct = (int)Math.Round(e.NewValue);
        AuxVolumeLabel.Text = $"{pct}%";
        _router.SetVolume(_auxEndpoint.Id, pct);
    }

    private void BtVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _selectedBtEndpoint is not { IsActive: true }) return;
        int pct = (int)Math.Round(e.NewValue);
        BtVolumeLabel.Text = $"{pct}%";
        _router.SetVolume(_selectedBtEndpoint.Id, pct);
    }

    private void AuxMicSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _auxMicEndpoint is not { IsActive: true }) return;
        int pct = (int)Math.Round(e.NewValue);
        AuxMicVolLabel.Text = $"{pct}%";
        _router.SetVolume(_auxMicEndpoint.Id, pct);
    }

    private void BtMicSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _selectedBtMicEndpoint is not { IsActive: true }) return;
        int pct = (int)Math.Round(e.NewValue);
        BtMicVolLabel.Text = $"{pct}%";
        _router.SetVolume(_selectedBtMicEndpoint.Id, pct);
    }

    private void BuiltInMicSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _builtInMicEndpoint is not { IsActive: true }) return;
        int pct = (int)Math.Round(e.NewValue);
        BuiltInMicVolLabel.Text = $"{pct}%";
        _router.SetVolume(_builtInMicEndpoint.Id, pct);
    }

    private void SyncDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _isUpdatingUi || SyncDelayLabel == null) return;
        int ms = (int)Math.Round(e.NewValue);
        if (_mirrorHasBtAndWired) _btWiredDelayMs = ms;
        else _wiredDelayMs = ms;
        _mirror.SyncDelayMs = ms;
        SyncDelayLabel.Text = $"{ms} ms";
    }

    private void SpeakersVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUi || _speakersEndpoint is not { IsActive: true }) return;
        int pct = (int)Math.Round(e.NewValue);
        SpeakersVolumeLabel.Text = $"{pct}%";
        _router.SetVolume(_speakersEndpoint.Id, pct);
    }

    private void ToggleEndpointMute(AudioEndpointInfo? endpoint, ToggleButton toggle, string label)
    {
        if (_isUpdatingUi || endpoint is not { IsActive: true }) return;
        bool mute = toggle.IsChecked == true;
        _router.SetMute(endpoint.Id, mute);
        SetStatus(mute ? $"Muted {label}." : $"Unmuted {label}.");
        RefreshDevices();
    }

    private void AuxMuteToggle_Click(object sender, RoutedEventArgs e) =>
        ToggleEndpointMute(_auxEndpoint, AuxMuteToggle, _auxEndpoint?.FriendlyName ?? "AUX");

    private void BtMuteToggle_Click(object sender, RoutedEventArgs e) =>
        ToggleEndpointMute(_selectedBtEndpoint, BtMuteToggle, _selectedBtEndpoint?.FriendlyName ?? "Bluetooth");

    private void SpeakersMuteToggle_Click(object sender, RoutedEventArgs e) =>
        ToggleEndpointMute(_speakersEndpoint, SpeakersMuteToggle, _speakersEndpoint?.FriendlyName ?? "Speakers");

    private void MultiRowMuteToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi || sender is not ToggleButton btn || btn.Tag is not string id) return;
        bool mute = btn.IsChecked == true;
        _router.SetMute(id, mute);
        var opt = _multiOptions.FirstOrDefault(o => o.Id == id);
        SetStatus(mute ? $"Muted {opt?.ShortLabel ?? "device"}." : $"Unmuted {opt?.ShortLabel ?? "device"}.");
        RefreshDevices();
    }

    private void AuxMicMuteToggle_Click(object sender, RoutedEventArgs e) =>
        ToggleEndpointMute(_auxMicEndpoint, AuxMicMuteToggle, _auxMicEndpoint?.FriendlyName ?? "AUX Mic");

    private void BtMicMuteToggle_Click(object sender, RoutedEventArgs e) =>
        ToggleEndpointMute(_selectedBtMicEndpoint, BtMicMuteToggle, _selectedBtMicEndpoint?.FriendlyName ?? "Bluetooth Mic");

    private void BuiltInMicMuteToggle_Click(object sender, RoutedEventArgs e) =>
        ToggleEndpointMute(_builtInMicEndpoint, BuiltInMicMuteToggle, _builtInMicEndpoint?.FriendlyName ?? "Laptop Mic");

    private void OtherMicMuteToggle_Click(object sender, RoutedEventArgs e) =>
        ToggleEndpointMute(_selectedOtherMicEndpoint, OtherMicMuteToggle, _selectedOtherMicEndpoint?.FriendlyName ?? "Other Mic");

    private void ShowAuxMicCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;
        _settings.ShowAuxJackMic = ShowAuxMicCheck.IsChecked == true;
        _settings.Save();
        RefreshDevices();
    }

    private void AutoMuteCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;
        _settings.MuteInactiveDevices = AutoMuteCheck.IsChecked == true;
        _settings.Save();
    }

    // --- Selectors ---
    private void BtDeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;
        int idx = BtDeviceSelector.SelectedIndex;
        if (idx >= 0 && idx < _btEndpoints.Count)
        {
            _selectedBtEndpoint = _btEndpoints[idx];
            _preferredBtId = _selectedBtEndpoint.Id;
            RefreshDevices();
        }
    }

    private void BtMicSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;
        int idx = BtMicSelector.SelectedIndex;
        if (idx >= 0 && idx < _btMicEndpoints.Count)
        {
            _selectedBtMicEndpoint = _btMicEndpoints[idx];
            _preferredBtMicId = _selectedBtMicEndpoint.Id;
            RefreshDevices();
        }
    }

    private void OtherMicSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;
        int idx = OtherMicSelector.SelectedIndex;
        if (idx >= 0 && idx < _otherMicEndpoints.Count)
        {
            _selectedOtherMicEndpoint = _otherMicEndpoints[idx];
            _preferredOtherMicId = _selectedOtherMicEndpoint.Id;
            RefreshDevices();
        }
    }

    private void ComboBox_DropDownClosed(object? sender, EventArgs e)
    {
        if (!ShouldStayVisible())
        {
            _collapseTimer.Stop();
            _collapseTimer.Start();
        }
    }

    private async void BtConnectSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBtEndpoint != null)
        {
            SetStatus($"Connecting {_selectedBtEndpoint.ShortName}...");
            var res = await _router.TryConnectBluetoothAudioAsync(_selectedBtEndpoint.ShortName);
            await Task.Delay(400);
            RefreshDevices();
            if (res.Success)
            {
                await ActivateBluetoothAsync();
                return;
            }
            SetStatus($"{res.Message} Opening Bluetooth settings...");
        }
        OpenBluetoothSettings();
    }

    private void HardDisableAuxCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;
        _settings.HardDisableAux = HardDisableAuxCheck.IsChecked == true;
        _settings.Save();
        if (HardDisableAuxCheck.IsChecked == false && _auxEndpoint is { IsDisabled: true })
        {
            _router.SetEndpointVisibility(_auxEndpoint.Id, true);
            RefreshDevices();
            SetStatus("AUX endpoint re-enabled in Windows.");
        }
    }

    private async Task RefreshTelemetryAsync()
    {
        if (_telemetryRefreshInFlight) return;
        _telemetryRefreshInFlight = true;
        try
        {
            var snap = await Task.Run(() => _telemetry.CaptureSnapshot(includeBluetoothBattery: true));
            if (!IsLoaded) return;

            var emerald = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
            var rose = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
            var blue = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA));

            if (_settings.ShowLaptopBattery && snap.HasLaptopBattery)
            {
                LaptopRingCard.Visibility = Visibility.Visible;
                var laptopBrush = (snap.LaptopBatteryPercent <= 20 && !snap.IsPluggedIn) ? rose : emerald;

                DeckLaptopRing.Percent = snap.LaptopBatteryPercent;
                DeckLaptopRing.DisplayText = $"{snap.LaptopBatteryPercent}%";
                DeckLaptopRing.RingBrush = laptopBrush;
                DeckLaptopSubText.Text = snap.IsPluggedIn ? "Charging" : "On Battery";

                PillLaptopRing.Percent = snap.LaptopBatteryPercent;
                PillLaptopRing.RingBrush = laptopBrush;
                PillLaptopPctText.Text = snap.IsPluggedIn ? $"⚡{snap.LaptopBatteryPercent}%" : $"{snap.LaptopBatteryPercent}%";
                if (System.Windows.Application.Current.TryFindResource(snap.IsPluggedIn ? "IconBolt" : "IconLaptop") is Geometry lg)
                {
                    DeckLaptopRing.GlyphData = lg;
                    PillLaptopRing.GlyphData = lg;
                }
            }
            else
            {
                LaptopRingCard.Visibility = Visibility.Collapsed;
            }

            if (_settings.ShowBluetoothBatteries && snap.BluetoothBatteries.Count > 0)
            {
                DeckBtBatteryRings.Visibility = Visibility.Visible;
                DeckBtBatteryRings.ItemsSource = snap.BluetoothBatteries
                    .Select(b => new BtRingItem
                    {
                        Name = b.DeviceName,
                        Percent = b.Percent,
                        PercentText = $"{b.Percent}%"
                    })
                    .ToList();

                var firstBt = snap.BluetoothBatteries[0];
                PillBtRingGroup.Visibility = _settings.ShowPillBattery ? Visibility.Visible : Visibility.Collapsed;
                PillBtRing.Percent = firstBt.Percent;
                PillBtRing.RingBrush = firstBt.Percent <= 20 ? rose : blue;
                PillBtPctText.Text = $"{firstBt.Percent}%";
            }
            else
            {
                DeckBtBatteryRings.Visibility = Visibility.Collapsed;
                PillBtRingGroup.Visibility = Visibility.Collapsed;
            }

            if (_settings.ShowSystemStats)
            {
                DeckRamRing.Percent = snap.RamUsagePercent;
                DeckRamRing.DisplayText = $"{snap.RamUsagePercent}%";
                DeckCpuRing.Percent = snap.SystemCpuPercent;
                DeckCpuRing.DisplayText = $"{snap.SystemCpuPercent}%";
                DeckClockText.Text = DateTime.Now.ToString("h:mm tt");
                DeckDateText.Text = DateTime.Now.ToString("ddd, MMM d");
            }
        }
        finally
        {
            _telemetryRefreshInFlight = false;
        }
    }

    private void UpdateMediaUi(MediaSessionSnapshot snap)
    {
        _lastMedia = snap;

        if (!snap.HasSession)
        {
            MediaSourceBadge.Visibility = Visibility.Collapsed;
            MediaStatusTag.Text = "No active media";
            MediaTitleText.Text = "Play Spotify, YouTube, or any media";
            MediaArtistText.Text = "Live controls appear automatically";
            MediaAlbumArt.Visibility = Visibility.Collapsed;
            MediaFallbackIcon.Visibility = Visibility.Visible;
            PillMediaArt.Visibility = Visibility.Collapsed;
            PillMediaFallbackIcon.Visibility = Visibility.Visible;
            MediaPrevBtn.IsEnabled = false;
            MediaPlayPauseBtn.IsEnabled = false;
            MediaNextBtn.IsEnabled = false;
            PillPlayBtn.IsEnabled = false;
            SetPlayIcons(isPlaying: false);
        }
        else
        {
            MediaSourceBadge.Visibility = Visibility.Visible;
            MediaSourceText.Text = snap.SourceApp;
            bool isSpotify = snap.SourceApp.Contains("Spotify", StringComparison.OrdinalIgnoreCase);
            MediaSourceBadge.Background = new SolidColorBrush(isSpotify
                ? Color.FromRgb(0x14, 0x53, 0x2D)
                : Color.FromRgb(0x1E, 0x3A, 0x8A));
            MediaSourceText.Foreground = new SolidColorBrush(isSpotify
                ? Color.FromRgb(0x4A, 0xDE, 0x80)
                : Color.FromRgb(0x93, 0xC5, 0xFD));

            MediaStatusTag.Text = snap.IsPlaying ? "Now Playing" : "Paused";
            MediaTitleText.Text = snap.Title;
            MediaArtistText.Text = string.IsNullOrWhiteSpace(snap.Artist) ? snap.SourceApp : snap.Artist;
            MediaPrevBtn.IsEnabled = true;
            MediaPlayPauseBtn.IsEnabled = true;
            MediaNextBtn.IsEnabled = true;
            PillPlayBtn.IsEnabled = true;
            SetPlayIcons(snap.IsPlaying);

            if (snap.Thumbnail != null)
            {
                MediaAlbumArt.Source = snap.Thumbnail;
                MediaAlbumArt.Visibility = Visibility.Visible;
                MediaFallbackIcon.Visibility = Visibility.Collapsed;
                PillMediaArt.Source = snap.Thumbnail;
                PillMediaArt.Visibility = Visibility.Visible;
                PillMediaFallbackIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                MediaAlbumArt.Visibility = Visibility.Collapsed;
                MediaFallbackIcon.Visibility = Visibility.Visible;
                PillMediaArt.Visibility = Visibility.Collapsed;
                PillMediaFallbackIcon.Visibility = Visibility.Visible;
            }
        }

        UpdateCompactPillMediaAndRoute();
        UpdateMediaProgressVisuals();
        UpdateMediaProgressState();
        if (_activePage == IslandPage.Media)
        {
            UpdateHeaderForPage();
        }
    }

    private void UpdateMediaProgressState()
    {
        bool shouldTick = _state != IslandState.ThinLine
                          && _activePage == IslandPage.Media
                          && _lastMedia.HasSession
                          && _lastMedia.IsPlaying
                          && _lastMedia.Duration > TimeSpan.Zero;

        if (shouldTick)
        {
            if (!_mediaProgressTimer.IsEnabled)
            {
                _mediaProgressTimer.Start();
            }
        }
        else if (_mediaProgressTimer.IsEnabled)
        {
            _mediaProgressTimer.Stop();
        }
    }

    private void UpdateMediaProgressVisuals()
    {
        bool isSpotify = !_lastMedia.HasSession || _lastMedia.SourceApp.Contains("Spotify", StringComparison.OrdinalIgnoreCase);
        MediaProgressStart.Color = isSpotify ? Color.FromRgb(0x22, 0xC5, 0x5E) : Color.FromRgb(0x3B, 0x82, 0xF6);
        MediaProgressEnd.Color = isSpotify ? Color.FromRgb(0x4A, 0xDE, 0x80) : Color.FromRgb(0x60, 0xA5, 0xFA);
        PillMediaProgressFill.Background = new SolidColorBrush(MediaProgressEnd.Color);

        if (!_lastMedia.HasSession || _lastMedia.Duration <= TimeSpan.Zero)
        {
            MediaProgressScale.ScaleX = 0;
            PillMediaProgressScale.ScaleX = 0;
            MediaElapsedText.Text = "0:00";
            MediaDurationText.Text = _lastMedia.HasSession ? "Live" : "--:--";
            return;
        }

        TimeSpan pos = _lastMedia.Position;
        if (_lastMedia.IsPlaying && _lastMedia.LastUpdatedTime != DateTimeOffset.MinValue)
        {
            TimeSpan elapsedSinceUpdate = DateTimeOffset.UtcNow - _lastMedia.LastUpdatedTime;
            if (elapsedSinceUpdate > TimeSpan.Zero)
            {
                double rate = _lastMedia.PlaybackRate > 0 ? _lastMedia.PlaybackRate : 1.0;
                pos += TimeSpan.FromTicks((long)(elapsedSinceUpdate.Ticks * rate));
            }
        }

        if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
        if (pos > _lastMedia.Duration) pos = _lastMedia.Duration;

        double ratio = Math.Clamp(pos.TotalSeconds / _lastMedia.Duration.TotalSeconds, 0.0, 1.0);
        MediaProgressScale.ScaleX = ratio;
        PillMediaProgressScale.ScaleX = ratio;
        MediaElapsedText.Text = FormatMediaTime(pos);
        MediaDurationText.Text = FormatMediaTime(_lastMedia.Duration);
    }

    private static string FormatMediaTime(TimeSpan ts)
    {
        if (ts.TotalHours >= 1.0)
        {
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }

    private void SetPlayIcons(bool isPlaying)
    {
        if (System.Windows.Application.Current.TryFindResource(isPlaying ? "IconPause" : "IconPlay") is Geometry geo)
        {
            MediaPlayPauseIcon.Data = geo;
            PillPlayIcon.Data = geo;
        }
    }

    private void UpdateCompactPillMediaAndRoute()
    {
        PillMediaTitle.Text = _lastMedia.HasSession ? _lastMedia.Title : "Nothing Playing";
        PillMediaArtist.Text = _lastMedia.HasSession
            ? (string.IsNullOrWhiteSpace(_lastMedia.Artist) ? _lastMedia.SourceApp : _lastMedia.Artist)
            : "Spotify / Media";

        if (_settings.ShowPillMediaTitle && _lastMedia.HasSession && _lastMedia.IsPlaying && !_mirror.IsRunning)
        {
            PillRouteText.Text = _lastMedia.Title;
        }
        else
        {
            PillRouteText.Text = _lastRouteLabel;
        }
    }

    private void SyncCheckboxesFromSettings()
    {
        _isUpdatingUi = true;
        try
        {
            OptMediaPlayerCheck.IsChecked = _settings.ShowMediaPlayer;
            OptLaptopBatCheck.IsChecked = _settings.ShowLaptopBattery;
            OptBtBatCheck.IsChecked = _settings.ShowBluetoothBatteries;
            OptSysStatsCheck.IsChecked = _settings.ShowSystemStats;
            OptSpeakersCheck.IsChecked = _settings.ShowSpeakersCard;
            OptMultiOutputCheck.IsChecked = _settings.ShowMultiOutput;
            OptPillEqCheck.IsChecked = _settings.ShowPillEqualizer;
            OptPillMediaCheck.IsChecked = _settings.ShowPillMediaTitle;
            OptPillBatCheck.IsChecked = _settings.ShowPillBattery;

            AutoMuteCheck.IsChecked = _settings.MuteInactiveDevices;
            HardDisableAuxCheck.IsChecked = _settings.HardDisableAux;
            ShowAuxMicCheck.IsChecked = _settings.ShowAuxJackMic;
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void ApplySettingsToUi()
    {
        EqWrap1.Visibility = _settings.ShowPillEqualizer ? Visibility.Visible : Visibility.Collapsed;
        EqWrap2.Visibility = _settings.ShowPillEqualizer ? Visibility.Visible : Visibility.Collapsed;
        EqWrap3.Visibility = _settings.ShowPillEqualizer ? Visibility.Visible : Visibility.Collapsed;

        var enabled = GetEnabledPages();
        if (!enabled.Contains(_activePage))
        {
            SwitchIslandPage(enabled[0], 0);
        }
        else
        {
            UpdatePageIndicatorVisuals();
        }

        UpdateCompactPillMediaAndRoute();
    }

    private void CustomizeBtn_Click(object sender, RoutedEventArgs e)
    {
        ToggleCustomizeView(!_isCustomizing);
    }

    private void CustomizeDoneBtn_Click(object sender, RoutedEventArgs e)
    {
        ToggleCustomizeView(false);
    }

    private void ToggleCustomizeView(bool show)
    {
        if (_isCustomizing == show) return;
        _isCustomizing = show;

        CustomizeBtn.Foreground = show
            ? new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA))
            : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

        var quart = new QuarticEase { EasingMode = EasingMode.EaseOut };
        if (show)
        {
            IslandSwitcherStrip.Visibility = Visibility.Collapsed;
            MainDeckHost.Visibility = Visibility.Collapsed;
            CustomizePanel.Visibility = Visibility.Visible;
            CustomizePanel.BeginAnimation(OpacityProperty, SmoothAnim(0.2, 1.0, 160, quart));
            CustomizePanelSlide.BeginAnimation(TranslateTransform.XProperty, SmoothAnim(22, 0, 175, quart));
        }
        else
        {
            CustomizePanel.Visibility = Visibility.Collapsed;
            IslandSwitcherStrip.Visibility = Visibility.Visible;
            MainDeckHost.Visibility = Visibility.Visible;
            MainDeckHost.BeginAnimation(OpacityProperty, SmoothAnim(0.2, 1.0, 160, quart));
            DeckCarouselSlide.BeginAnimation(TranslateTransform.XProperty, SmoothAnim(-22, 0, 175, quart));
        }
        UpdateHeaderForPage();
        SpringExpandedHeightToFit();
    }

    private void CustomizeOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;

        _settings.ShowMediaPlayer = OptMediaPlayerCheck.IsChecked == true;
        _settings.ShowLaptopBattery = OptLaptopBatCheck.IsChecked == true;
        _settings.ShowBluetoothBatteries = OptBtBatCheck.IsChecked == true;
        _settings.ShowSystemStats = OptSysStatsCheck.IsChecked == true;
        _settings.ShowSpeakersCard = OptSpeakersCheck.IsChecked == true;
        _settings.ShowMultiOutput = OptMultiOutputCheck.IsChecked == true;
        _settings.ShowPillEqualizer = OptPillEqCheck.IsChecked == true;
        _settings.ShowPillMediaTitle = OptPillMediaCheck.IsChecked == true;
        _settings.ShowPillBattery = OptPillBatCheck.IsChecked == true;

        _settings.Save();
        ApplySettingsToUi();
        RefreshDevices();
        _ = RefreshTelemetryAsync();
    }

    private void ResetCustomizeBtn_Click(object sender, RoutedEventArgs e)
    {
        _settings = new IslandSettings();
        _settings.Save();
        SyncCheckboxesFromSettings();
        ApplySettingsToUi();
        RefreshDevices();
        _ = RefreshTelemetryAsync();
        SetStatus("Restored default Dynamic Island widgets.");
    }

    private async void MediaPrevBtn_Click(object sender, RoutedEventArgs e) => await _media.PreviousAsync();
    private async void MediaPlayPauseBtn_Click(object sender, RoutedEventArgs e) => await _media.TogglePlayPauseAsync();
    private async void MediaNextBtn_Click(object sender, RoutedEventArgs e) => await _media.NextAsync();

    private static void OpenBluetoothSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch
        {
            // Ignore shell launch error
        }
    }

    private void SetStatus(string text) => StatusBanner.Text = text;
}
