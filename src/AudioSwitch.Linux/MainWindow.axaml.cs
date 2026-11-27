using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AudioSwitch.Linux.LinuxCore;

namespace AudioSwitch.Linux;

public partial class MainWindow : Window
{
    private enum IslandState { ThinLine, CompactPill, ExpandedDeck }
    private enum IslandPage { Audio = 0, Media = 1, Battery = 2 }
    private enum AudioSubTab { Outputs = 0, Mics = 1, Apps = 2 }

    private sealed record SinkChoice(string Name, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed class AppStreamRowVm
    {
        public uint StreamIndex { get; init; }
        public string AppName { get; init; } = string.Empty;
        public string RouteLabel { get; init; } = string.Empty;
        public IReadOnlyList<SinkChoice> Options { get; init; } = Array.Empty<SinkChoice>();
        public int SelectedIndex { get; init; }
    }

    private sealed class BtBatteryRowVm
    {
        public string DisplayText { get; init; } = string.Empty;
    }

    private readonly PipeWireAudioService _audio = new();
    private readonly MprisMediaService _mpris = new();
    private readonly LinuxMusicLauncherService _launcher = new();
    private readonly LinuxTelemetryService _telemetry = new();
    private readonly DispatcherTimer _collapseTimer = new();
    private readonly DispatcherTimer _telemetryTimer = new();

    private LinuxSettings _settings = new();
    private IslandState _state = IslandState.ThinLine;
    private IslandPage _page = IslandPage.Audio;
    private AudioSubTab _subTab = AudioSubTab.Outputs;

    private IReadOnlyList<LinuxAudioEndpoint> _sinks = Array.Empty<LinuxAudioEndpoint>();
    private IReadOnlyList<LinuxAudioEndpoint> _sources = Array.Empty<LinuxAudioEndpoint>();
    private LinuxAudioEndpoint? _auxSink;
    private LinuxAudioEndpoint? _btSink;
    private LinuxAudioEndpoint? _speakersSink;
    private LinuxAudioEndpoint? _btMic;
    private LinuxAudioEndpoint? _builtInMic;

    private bool _isUpdatingUi;

    public MainWindow()
    {
        InitializeComponent();

        _collapseTimer.Interval = TimeSpan.FromMilliseconds(260);
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            CollapseToThinLine();
        };

        _telemetryTimer.Interval = TimeSpan.FromSeconds(2);
        _telemetryTimer.Tick += (_, _) => RefreshTelemetry();

        _mpris.MediaUpdated += snap => Dispatcher.UIThread.Post(() => ApplyMediaSnapshot(snap));

        Opened += (_, _) =>
        {
            _settings = LinuxSettings.Load();
            _isUpdatingUi = true;
            AutoMuteCheck.IsChecked = _settings.MuteInactiveDevices;
            HideMusicCheck.IsChecked = _settings.HideMusicToTray;
            DelaySlider.Value = _settings.SyncDelayMs;
            DelayLabel.Text = $"{_settings.SyncDelayMs} ms";
            _isUpdatingUi = false;

            _mpris.SetPreferredProvider(_settings.UseYouTubeMusicDefault);
            _mpris.StartMonitoring();
            PositionTopCenter();
            RefreshDevices();
            UpdateMusicProviderButtons();
        };

        Deactivated += (_, _) =>
        {
            if (_state != IslandState.ThinLine)
            {
                CollapseToThinLine();
            }
        };

        Closed += (_, _) =>
        {
            _audio.StopMirror();
            _mpris.Dispose();
            _collapseTimer.Stop();
            _telemetryTimer.Stop();
        };
    }

    public void ExpandToDeckFromTray()
    {
        Activate();
        SetState(IslandState.ExpandedDeck);
    }

    public void ToggleOpenOrCollapse()
    {
        if (_state == IslandState.ExpandedDeck) CollapseToThinLine();
        else ExpandToDeckFromTray();
    }

    public void CollapseToThinLine()
    {
        _collapseTimer.Stop();
        SetState(IslandState.ThinLine);
    }

    public void QuickSwapAuxBt()
    {
        bool auxDefault = _auxSink is { IsDefault: true };
        var target = auxDefault ? _btSink : _auxSink;
        if (target == null) return;
        _audio.SwitchDefaultSink(target.Name, AutoMuteCheck.IsChecked == true);
        RefreshDevices();
    }

    private void PositionTopCenter()
    {
        var screen = Screens.Primary;
        if (screen == null) return;
        double scale = screen.Scaling > 0 ? screen.Scaling : 1.0;
        double logicalWidth = screen.WorkingArea.Width / scale;
        double x = (screen.WorkingArea.X / scale) + (logicalWidth - Width) / 2.0;
        double y = screen.WorkingArea.Y / scale;
        Position = new PixelPoint((int)Math.Round(x * scale), (int)Math.Round(y * scale));
    }

    private void SetState(IslandState next)
    {
        _state = next;
        ThinLineView.IsVisible = next == IslandState.ThinLine;
        CompactPillView.IsVisible = next == IslandState.CompactPill;
        ExpandedDeckView.IsVisible = next == IslandState.ExpandedDeck;

        IslandShell.Width = next switch
        {
            IslandState.ThinLine => 120,
            IslandState.CompactPill => 300,
            _ => 356
        };

        if (next == IslandState.ThinLine)
        {
            _telemetryTimer.Stop();
        }
        else
        {
            RefreshDevices();
            if (next == IslandState.ExpandedDeck)
            {
                RefreshTelemetry();
                _telemetryTimer.Start();
            }
            else
            {
                _telemetryTimer.Stop();
            }
        }
    }

    private void IslandShell_PointerEntered(object? sender, PointerEventArgs e)
    {
        _collapseTimer.Stop();
        if (_state == IslandState.ThinLine)
        {
            SetState(IslandState.CompactPill);
        }
    }

    private void IslandShell_PointerExited(object? sender, PointerEventArgs e)
    {
        if (_state == IslandState.CompactPill)
        {
            _collapseTimer.Start();
        }
    }

    private void ThinLine_PointerPressed(object? sender, PointerPressedEventArgs e) => ExpandToDeckFromTray();
    private void CompactPill_PointerPressed(object? sender, PointerPressedEventArgs e) => ExpandToDeckFromTray();
    private void CollapseBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CollapseToThinLine();
    private void SwapBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => QuickSwapAuxBt();

    private void NavAudio_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SwitchPage(IslandPage.Audio);
    private void NavMedia_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SwitchPage(IslandPage.Media);
    private void NavBattery_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SwitchPage(IslandPage.Battery);

    private void SwitchPage(IslandPage page)
    {
        _page = page;
        AudioPage.IsVisible = page == IslandPage.Audio;
        MediaPage.IsVisible = page == IslandPage.Media;
        BatteryPage.IsVisible = page == IslandPage.Battery;

        NavAudioBtn.Background = page == IslandPage.Audio ? SolidColorBrush.Parse("#283B82F6") : Brushes.Transparent;
        NavMediaBtn.Background = page == IslandPage.Media ? SolidColorBrush.Parse("#2822C55E") : Brushes.Transparent;
        NavBatteryBtn.Background = page == IslandPage.Battery ? SolidColorBrush.Parse("#2810B981") : Brushes.Transparent;

        NavAudioBtn.Foreground = page == IslandPage.Audio ? Brushes.White : SolidColorBrush.Parse("#94A3B8");
        NavMediaBtn.Foreground = page == IslandPage.Media ? Brushes.White : SolidColorBrush.Parse("#94A3B8");
        NavBatteryBtn.Foreground = page == IslandPage.Battery ? Brushes.White : SolidColorBrush.Parse("#94A3B8");

        if (page == IslandPage.Battery) RefreshTelemetry();
    }

    private void SubOutputs_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SwitchSubTab(AudioSubTab.Outputs);
    private void SubMics_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SwitchSubTab(AudioSubTab.Mics);
    private void SubApps_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SwitchSubTab(AudioSubTab.Apps);

    private void SwitchSubTab(AudioSubTab sub)
    {
        _subTab = sub;
        OutputsSubPanel.IsVisible = sub == AudioSubTab.Outputs;
        MicsSubPanel.IsVisible = sub == AudioSubTab.Mics;
        AppsSubPanel.IsVisible = sub == AudioSubTab.Apps;

        SubOutputsBtn.Background = sub == AudioSubTab.Outputs ? SolidColorBrush.Parse("#2563EB") : Brushes.Transparent;
        SubMicsBtn.Background = sub == AudioSubTab.Mics ? SolidColorBrush.Parse("#2563EB") : Brushes.Transparent;
        SubAppsBtn.Background = sub == AudioSubTab.Apps ? SolidColorBrush.Parse("#2563EB") : Brushes.Transparent;

        SubOutputsBtn.Foreground = sub == AudioSubTab.Outputs ? Brushes.White : SolidColorBrush.Parse("#94A3B8");
        SubMicsBtn.Foreground = sub == AudioSubTab.Mics ? Brushes.White : SolidColorBrush.Parse("#94A3B8");
        SubAppsBtn.Foreground = sub == AudioSubTab.Apps ? Brushes.White : SolidColorBrush.Parse("#94A3B8");

        if (sub == AudioSubTab.Apps) RefreshAppStreams();
    }

    private void RefreshDevices()
    {
        _isUpdatingUi = true;
        try
        {
            _sinks = _audio.GetSinks();
            _sources = _audio.GetSources();

            _auxSink = _sinks.FirstOrDefault(s => s.Category == LinuxAudioCategory.AuxWired);
            _btSink = _sinks.FirstOrDefault(s => s.Category == LinuxAudioCategory.Bluetooth);
            _speakersSink = _sinks.FirstOrDefault(s => s.Category == LinuxAudioCategory.BuiltInSpeakers);

            _btMic = _sources.FirstOrDefault(s => s.Category == LinuxAudioCategory.Bluetooth);
            _builtInMic = _sources.FirstOrDefault(s => s.Category == LinuxAudioCategory.BuiltInMic || s.Category == LinuxAudioCategory.AuxWired);

            AuxNameText.Text = _auxSink?.FriendlyName ?? "No AUX headphone detected";
            AuxToggle.IsEnabled = _auxSink != null;
            AuxToggle.IsChecked = _auxSink is { IsDefault: true, IsMuted: false };
            AuxVolSlider.Value = _auxSink?.VolumePercent ?? 0;
            AuxVolLabel.Text = $"{_auxSink?.VolumePercent ?? 0}%";

            BtNameText.Text = _btSink?.FriendlyName ?? "No Bluetooth output connected";
            BtToggle.IsEnabled = _btSink != null;
            BtToggle.IsChecked = _btSink is { IsDefault: true, IsMuted: false };
            BtVolSlider.Value = _btSink?.VolumePercent ?? 0;
            BtVolLabel.Text = $"{_btSink?.VolumePercent ?? 0}%";

            SpeakersCard.IsVisible = _speakersSink != null;
            SpeakersNameText.Text = _speakersSink?.FriendlyName ?? "Built-in Speakers";
            SpeakersToggle.IsChecked = _speakersSink is { IsDefault: true, IsMuted: false };

            BtMicText.Text = _btMic?.FriendlyName ?? "No Bluetooth mic";
            BtMicToggle.IsEnabled = _btMic != null;
            BtMicToggle.IsChecked = _btMic is { IsDefault: true, IsMuted: false };

            BuiltInMicText.Text = _builtInMic?.FriendlyName ?? "Internal Microphone";
            BuiltInMicToggle.IsEnabled = _builtInMic != null;
            BuiltInMicToggle.IsChecked = _builtInMic is { IsDefault: true, IsMuted: false };

            var def = _sinks.FirstOrDefault(s => s.IsDefault);
            PillTitleText.Text = def != null ? def.FriendlyName : "No Output";
            HeaderSubText.Text = _audio.IsMirroring ? "Mirroring AUX + Bluetooth" : (def?.FriendlyName ?? "PipeWire");
            MirrorBtn.Content = _audio.IsMirroring ? "Stop" : "Sync";

            if (_subTab == AudioSubTab.Apps && _state == IslandState.ExpandedDeck)
            {
                RefreshAppStreams();
            }
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void AuxToggle_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdatingUi || _auxSink == null) return;
        if (AuxToggle.IsChecked == true)
        {
            _audio.StopMirror();
            _audio.SwitchDefaultSink(_auxSink.Name, AutoMuteCheck.IsChecked == true);
        }
        else if (_btSink != null)
        {
            _audio.SwitchDefaultSink(_btSink.Name, AutoMuteCheck.IsChecked == true);
        }
        RefreshDevices();
    }

    private void BtToggle_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdatingUi || _btSink == null) return;
        if (BtToggle.IsChecked == true)
        {
            _audio.StopMirror();
            _audio.SwitchDefaultSink(_btSink.Name, AutoMuteCheck.IsChecked == true);
        }
        else if (_auxSink != null)
        {
            _audio.SwitchDefaultSink(_auxSink.Name, AutoMuteCheck.IsChecked == true);
        }
        RefreshDevices();
    }

    private void SpeakersToggle_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdatingUi || _speakersSink == null) return;
        if (SpeakersToggle.IsChecked == true)
        {
            _audio.StopMirror();
            _audio.SwitchDefaultSink(_speakersSink.Name, AutoMuteCheck.IsChecked == true);
        }
        RefreshDevices();
    }

    private void AuxVol_Changed(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingUi || _auxSink == null) return;
        int pct = (int)Math.Round(e.NewValue);
        AuxVolLabel.Text = $"{pct}%";
        _audio.SetVolume(_auxSink.Name, pct);
    }

    private void BtVol_Changed(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingUi || _btSink == null) return;
        int pct = (int)Math.Round(e.NewValue);
        BtVolLabel.Text = $"{pct}%";
        _audio.SetVolume(_btSink.Name, pct);
    }

    private void MirrorBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_audio.IsMirroring)
        {
            _audio.StopMirror();
            StatusBanner.Text = "Stopped Multi-Output mirroring.";
        }
        else if (_auxSink != null && _btSink != null)
        {
            int delay = (int)Math.Round(DelaySlider.Value);
            bool ok = _audio.StartMirror(_btSink.Name, [_auxSink.Name], delay);
            StatusBanner.Text = ok ? $"Mirroring AUX + Bluetooth ({delay} ms delay)" : "Loopback module unavailable.";
        }
        else
        {
            StatusBanner.Text = "Connect both AUX and Bluetooth to mirror.";
        }
        RefreshDevices();
    }

    private void DelaySlider_Changed(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        int delay = (int)Math.Round(e.NewValue);
        DelayLabel.Text = $"{delay} ms";
        if (_isUpdatingUi) return;
        _settings.SyncDelayMs = delay;
        _settings.Save();
        if (_audio.IsMirroring && _auxSink != null && _btSink != null)
        {
            _audio.StartMirror(_btSink.Name, [_auxSink.Name], delay);
        }
    }

    private void BtMicToggle_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdatingUi || _btMic == null) return;
        if (BtMicToggle.IsChecked == true)
        {
            _audio.SwitchDefaultSource(_btMic.Name, AutoMuteCheck.IsChecked == true);
        }
        else if (_builtInMic != null)
        {
            _audio.SwitchDefaultSource(_builtInMic.Name, AutoMuteCheck.IsChecked == true);
        }
        RefreshDevices();
    }

    private void BuiltInMicToggle_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdatingUi || _builtInMic == null) return;
        if (BuiltInMicToggle.IsChecked == true)
        {
            _audio.SwitchDefaultSource(_builtInMic.Name, AutoMuteCheck.IsChecked == true);
        }
        else if (_btMic != null)
        {
            _audio.SwitchDefaultSource(_btMic.Name, AutoMuteCheck.IsChecked == true);
        }
        RefreshDevices();
    }

    private void RefreshApps_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => RefreshAppStreams();

    private void DualSplitBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_auxSink == null || _btSink == null)
        {
            StatusBanner.Text = "Connect both AUX and Bluetooth first to split audio.";
            return;
        }
        var (mediaMoved, callsMoved) = _audio.AutoDualSplit(_auxSink.Name, _btSink.Name, _btMic?.Name);
        StatusBanner.Text = $"Split: {mediaMoved} music → AUX • {callsMoved} call → BT";
        RefreshAppStreams();
    }

    private void RefreshAppStreams()
    {
        _isUpdatingUi = true;
        try
        {
            var inputs = _audio.GetSinkInputs();
            var options = _sinks.Select(s => new SinkChoice(s.Name, s.FriendlyName)).ToList();
            var rows = new List<AppStreamRowVm>();

            foreach (var inp in inputs.Take(5))
            {
                int idx = options.FindIndex(o => o.Name.Equals(inp.CurrentSinkOrSourceName, StringComparison.OrdinalIgnoreCase));
                rows.Add(new AppStreamRowVm
                {
                    StreamIndex = inp.StreamIndex,
                    AppName = inp.AppName,
                    RouteLabel = idx >= 0 ? options[idx].Label : inp.CurrentSinkOrSourceName,
                    Options = options,
                    SelectedIndex = Math.Max(0, idx)
                });
            }

            AppStreamsList.ItemsSource = rows;
            AppsEmptyText.IsVisible = rows.Count == 0;
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    private void AppSinkCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;
        if (sender is not ComboBox combo) return;
        if (combo.Tag is not uint streamId) return;
        if (combo.SelectedItem is not SinkChoice choice) return;

        _audio.MoveAppOutput(streamId, choice.Name);
        StatusBanner.Text = $"Routed stream to {choice.Label}";
        RefreshAppStreams();
    }

    private void ApplyMediaSnapshot(LinuxMediaSnapshot snap)
    {
        string providerLabel = _settings.UseYouTubeMusicDefault ? "YouTube Music" : "Spotify";
        if (!snap.HasSession)
        {
            MediaStatusText.Text = "Ready";
            MediaTitleText.Text = $"Play {providerLabel}";
            MediaArtistText.Text = "Click ▶ to launch & play";
            MediaPlayPauseBtn.Content = "▶";
            PillPlayBtn.Content = "▶";
            PillSubText.Text = $"Play {providerLabel}";
            MediaProgress.Value = 0;
        }
        else
        {
            MediaStatusText.Text = snap.IsPlaying ? $"{snap.SourceApp} • Playing" : $"{snap.SourceApp} • Paused";
            MediaTitleText.Text = string.IsNullOrWhiteSpace(snap.Title) ? snap.SourceApp : snap.Title;
            MediaArtistText.Text = string.IsNullOrWhiteSpace(snap.Artist) ? snap.SourceApp : snap.Artist;
            MediaPlayPauseBtn.Content = snap.IsPlaying ? "⏸" : "▶";
            PillPlayBtn.Content = snap.IsPlaying ? "⏸" : "▶";
            PillSubText.Text = MediaTitleText.Text;
            MediaProgress.Value = snap.LengthMicros > 0
                ? Math.Clamp(snap.PositionMicros * 100.0 / snap.LengthMicros, 0, 100)
                : 0;
        }

        bool isYt = snap.HasSession
            ? snap.SourceApp.Contains("YouTube", StringComparison.OrdinalIgnoreCase)
            : _settings.UseYouTubeMusicDefault;
        MediaProgress.Foreground = SolidColorBrush.Parse(isYt ? "#EF4444" : "#22C55E");
        UpdateMusicProviderButtons();
    }

    private void UpdateMusicProviderButtons()
    {
        bool useYt = _settings.UseYouTubeMusicDefault;
        SpotifySwitchBtn.Background = SolidColorBrush.Parse(useYt ? "#24FFFFFF" : "#166534");
        YtMusicSwitchBtn.Background = SolidColorBrush.Parse(useYt ? "#991B1B" : "#24FFFFFF");
        PillProviderBtn.Content = useYt ? "YT" : "SP";
        PillProviderBtn.Foreground = SolidColorBrush.Parse(useYt ? "#FCA5A5" : "#4ADE80");
    }

    private async void SpotifySelect_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await SwitchProviderAsync(LinuxMusicKind.Spotify);

    private async void YtMusicSelect_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await SwitchProviderAsync(LinuxMusicKind.YouTubeMusic);

    private async void PillProviderBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var next = _settings.UseYouTubeMusicDefault ? LinuxMusicKind.Spotify : LinuxMusicKind.YouTubeMusic;
        await SwitchProviderAsync(next);
    }

    private async Task SwitchProviderAsync(LinuxMusicKind kind)
    {
        bool useYt = kind == LinuxMusicKind.YouTubeMusic;
        _settings.UseYouTubeMusicDefault = useYt;
        _settings.Save();
        _mpris.SetPreferredProvider(useYt);
        UpdateMusicProviderButtons();

        var (_, msg) = await _launcher.LaunchAndPlayAsync(
            kind,
            _mpris,
            HideMusicCheck.IsChecked == true,
            status => Dispatcher.UIThread.Post(() => StatusBanner.Text = status));
        StatusBanner.Text = msg;
    }

    private async void MediaPlayPauseBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var kind = _settings.UseYouTubeMusicDefault ? LinuxMusicKind.YouTubeMusic : LinuxMusicKind.Spotify;
        if (_mpris.HasSessionFor(kind))
        {
            _mpris.PauseCompetingPlayers(kind);
            _mpris.TogglePlayPause();
            return;
        }

        var (_, msg) = await _launcher.LaunchAndPlayAsync(
            kind,
            _mpris,
            HideMusicCheck.IsChecked == true,
            status => Dispatcher.UIThread.Post(() => StatusBanner.Text = status));
        StatusBanner.Text = msg;
    }

    private void MediaPrevBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _mpris.Previous();
    private void MediaNextBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _mpris.Next();

    private void MusicHide_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var kind = _settings.UseYouTubeMusicDefault ? LinuxMusicKind.YouTubeMusic : LinuxMusicKind.Spotify;
        bool shouldHide = MusicHideBtn.Content?.ToString() == "Hide";
        LinuxMusicLauncherService.ToggleWindowOnX11(kind, shouldHide);
        MusicHideBtn.Content = shouldHide ? "Show" : "Hide";
    }

    private void RefreshTelemetry()
    {
        var snap = _telemetry.CaptureSnapshot();
        LaptopPowerText.Text = snap.HasLaptopBattery
            ? $"Laptop: {snap.LaptopBatteryPercent}% {(snap.IsPluggedIn ? "(Plugged In)" : "(Battery)")}"
            : "Power: AC / Desktop";

        BtBatteriesList.ItemsSource = snap.BluetoothBatteries
            .Select(b => new BtBatteryRowVm { DisplayText = $"🎧 {b.DeviceName}: {b.Percent}%" })
            .ToList();

        SysRamCpuText.Text = $"RAM: {snap.RamUsagePercent}% · CPU: {snap.CpuUsagePercent}%";
        SessionTypeText.Text = $"Session: {(LinuxMusicLauncherService.IsWaylandSession ? "Wayland (Tray Host)" : "X11 (Top-Edge Island)")}";
        ClockText.Text = DateTime.Now.ToString("h:mm tt");
    }

    private void SettingsCheck_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;
        _settings.MuteInactiveDevices = AutoMuteCheck.IsChecked == true;
        _settings.HideMusicToTray = HideMusicCheck.IsChecked == true;
        _settings.Save();
    }

    private void UninstallBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LinuxInstallerService.UninstallAll();
        Close();
    }
}
