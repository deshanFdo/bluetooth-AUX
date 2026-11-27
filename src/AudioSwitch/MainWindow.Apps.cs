using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AudioSwitch.AudioCore;

namespace AudioSwitch;

public partial class MainWindow
{
    private const int MaxDisplayedAppRoutes = 5;

    internal sealed record AppEndpointOption(string? EndpointId, string Label)
    {
        public override string ToString() => Label;
    }

    internal sealed class AppRouteRowVm
    {
        public string ProcessName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string SubtitleText { get; init; } = string.Empty;
        public Brush SubtitleBrush { get; init; } = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        public IReadOnlyList<AppEndpointOption> EndpointOptions { get; init; } = Array.Empty<AppEndpointOption>();
        public int SelectedOptionIndex { get; init; }
    }

    private void AppsTabBtn_Click(object sender, RoutedEventArgs e) => SwitchTab(AudioSubTab.Apps);

    private void RefreshAppsBtn_Click(object sender, RoutedEventArgs e)
    {
        RefreshAppRoutesPanel(reapplyPins: true);
        SpringExpandedHeightToFit();
        SetStatus("Scanned running audio and call apps.");
    }

    private void ResetAppRoutesBtn_Click(object sender, RoutedEventArgs e)
    {
        bool ok = _appRouter.ResetAllPersistedAppRoutes();
        if (!ok)
        {
            foreach (var app in _appRouter.GetRunningAudioApps())
            {
                _appRouter.SetAppRoute(app.ProcessName, EDataFlow.Render, null);
                _appRouter.SetAppRoute(app.ProcessName, EDataFlow.Capture, null);
            }
        }

        RefreshAppRoutesPanel(reapplyPins: false);
        RefreshDevices();
        SpringExpandedHeightToFit();
        SetStatus("Reset all app routes to Windows Default.");
    }

    private async void AutoSplitBtn_Click(object sender, RoutedEventArgs e)
    {
        // Ensure AUX is active if merely disabled in Windows
        if (_auxEndpoint is { IsDisabled: true })
        {
            _router.SetEndpointVisibility(_auxEndpoint.Id, true);
            RefreshDevices();
        }

        if (_auxEndpoint is not { IsActive: true })
        {
            SetStatus("Plug in AUX cable first to split music to AUX.");
            return;
        }

        // Connect Bluetooth if selected headset is paired/unplugged
        if (_selectedBtEndpoint is { IsDisabled: true })
        {
            _router.SetEndpointVisibility(_selectedBtEndpoint.Id, true);
            RefreshDevices();
        }

        if (_selectedBtEndpoint is { IsActive: false })
        {
            SetStatus($"Connecting {_selectedBtEndpoint.ShortName}...");
            var conn = await _router.TryConnectBluetoothAudioAsync(_selectedBtEndpoint.ShortName);
            await Task.Delay(450);
            RefreshDevices();
            if (!conn.Success && !_btEndpoints.Any(b => b.IsActive))
            {
                SetStatus(conn.Message);
                return;
            }
        }

        var btOut = (_selectedBtEndpoint?.IsActive == true ? _selectedBtEndpoint : null)
                    ?? _btEndpoints.FirstOrDefault(b => b.IsActive && !b.IsHandsFree)
                    ?? _btEndpoints.FirstOrDefault(b => b.IsActive);

        if (btOut == null)
        {
            SetStatus("Connect a Bluetooth headset first for calls.");
            return;
        }

        var btMic = (_selectedBtMicEndpoint?.IsActive == true ? _selectedBtMicEndpoint : null)
                    ?? _btMicEndpoints.FirstOrDefault(m => m.IsActive);

        // Unmute both AUX and BT so simultaneous streams are audible immediately
        _router.SetMute(_auxEndpoint.Id, false);
        _router.SetMute(btOut.Id, false);
        if (btMic != null) _router.SetMute(btMic.Id, false);

        var apps = _appRouter.GetRunningAudioApps();
        int routedMedia = 0;
        int routedCalls = 0;

        foreach (var app in apps)
        {
            if (AppAudioRouterService.IsCallProcess(app.ProcessName))
            {
                if (_appRouter.SetAppRoute(app.ProcessName, EDataFlow.Render, btOut.Id).Success)
                {
                    routedCalls++;
                }
                if (btMic != null)
                {
                    _appRouter.SetAppRoute(app.ProcessName, EDataFlow.Capture, btMic.Id);
                }
            }
            else if (AppAudioRouterService.IsMediaProcess(app.ProcessName) || app.IsAudioActive)
            {
                if (_appRouter.SetAppRoute(app.ProcessName, EDataFlow.Render, _auxEndpoint.Id).Success)
                {
                    routedMedia++;
                }
            }
        }

        RefreshAppRoutesPanel(reapplyPins: false);
        RefreshDevices();
        SpringExpandedHeightToFit();

        if (routedMedia == 0 && routedCalls == 0)
        {
            SetStatus("AUX & Bluetooth unmuted. Open Spotify/Chrome or Discord and assign below.");
        }
        else
        {
            SetStatus($"Split active: {routedMedia} music → AUX • {routedCalls} call → {btOut.ShortName}");
        }
    }

    private void AppRouteCombo_DropDownOpened(object? sender, EventArgs e) => _isAppRouteComboOpen = true;

    private void AppRouteCombo_DropDownClosed(object? sender, EventArgs e)
    {
        _isAppRouteComboOpen = false;
        if (!ShouldStayVisible())
        {
            _collapseTimer.Start();
        }
    }

    private void AppRouteCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi) return;
        if (sender is not ComboBox combo) return;
        if (combo.Tag is not string procName || string.IsNullOrWhiteSpace(procName)) return;
        if (combo.SelectedItem is not AppEndpointOption option) return;

        if (!string.IsNullOrEmpty(option.EndpointId))
        {
            var ep = FindRenderEndpointById(option.EndpointId);
            if (ep is { IsDisabled: true })
            {
                _router.SetEndpointVisibility(ep.Id, true);
                RefreshDevices();
            }
            _router.SetMute(option.EndpointId, false);
        }

        var res = _appRouter.SetAppRoute(procName, EDataFlow.Render, option.EndpointId);

        // If routing a call app (Discord/Teams/Zoom) to Bluetooth, pair its mic to the BT headset mic automatically
        if (res.Success && AppAudioRouterService.IsCallProcess(procName))
        {
            if (!string.IsNullOrEmpty(option.EndpointId) &&
                _btEndpoints.Any(b => string.Equals(b.Id, option.EndpointId, StringComparison.OrdinalIgnoreCase)))
            {
                var btMic = (_selectedBtMicEndpoint?.IsActive == true ? _selectedBtMicEndpoint : null)
                            ?? _btMicEndpoints.FirstOrDefault(m => m.IsActive);
                if (btMic != null)
                {
                    _router.SetMute(btMic.Id, false);
                    _appRouter.SetAppRoute(procName, EDataFlow.Capture, btMic.Id);
                }
            }
            else if (string.IsNullOrEmpty(option.EndpointId))
            {
                _appRouter.SetAppRoute(procName, EDataFlow.Capture, null);
            }
        }

        RefreshAppRoutesPanel(reapplyPins: false);
        SetStatus(res.Message);
    }

    private AudioEndpointInfo? FindRenderEndpointById(string endpointId)
    {
        if (_auxEndpoint != null && string.Equals(_auxEndpoint.Id, endpointId, StringComparison.OrdinalIgnoreCase)) return _auxEndpoint;
        if (_speakersEndpoint != null && string.Equals(_speakersEndpoint.Id, endpointId, StringComparison.OrdinalIgnoreCase)) return _speakersEndpoint;
        return _btEndpoints.FirstOrDefault(b => string.Equals(b.Id, endpointId, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshAppRoutesPanel(bool reapplyPins)
    {
        var prevUpdating = _isUpdatingUi;
        _isUpdatingUi = true;
        try
        {
            var apps = _appRouter.GetRunningAudioApps();
            if (reapplyPins)
            {
                _appRouter.ReapplyPinnedRoutesIfPresent(apps);
                apps = _appRouter.GetRunningAudioApps();
            }

            var options = BuildAppRenderOptions();
            var rows = new List<AppRouteRowVm>(MaxDisplayedAppRoutes);

            foreach (var app in apps.Take(MaxDisplayedAppRoutes))
            {
                int selectedIdx = 0;
                if (!string.IsNullOrEmpty(app.RoutedRenderEndpointId))
                {
                    int found = options.FindIndex(o =>
                        string.Equals(o.EndpointId, app.RoutedRenderEndpointId, StringComparison.OrdinalIgnoreCase));
                    if (found >= 0) selectedIdx = found;
                }

                string roleTag = AppAudioRouterService.IsCallProcess(app.ProcessName)
                    ? (!string.IsNullOrEmpty(app.RoutedCaptureEndpointId) ? "Call • Mic paired" : "Call app")
                    : app.IsAudioActive
                        ? "Playing audio"
                        : "Ready";

                bool isCustomRouted = selectedIdx > 0;
                string sub = isCustomRouted
                    ? $"{roleTag} → {options[selectedIdx].Label}"
                    : roleTag;

                rows.Add(new AppRouteRowVm
                {
                    ProcessName = app.ProcessName,
                    DisplayName = app.DisplayName,
                    SubtitleText = sub,
                    SubtitleBrush = new SolidColorBrush(
                        isCustomRouted
                            ? Color.FromRgb(0x34, 0xD3, 0x99)
                            : app.IsAudioActive
                                ? Color.FromRgb(0x38, 0xBD, 0xF8)
                                : Color.FromRgb(0x94, 0xA3, 0xB8)),
                    EndpointOptions = options,
                    SelectedOptionIndex = selectedIdx
                });
            }

            AppRouteItems.ItemsSource = rows;
            AppRoutesEmptyCard.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            bool anySplit = rows.Any(r => r.SelectedOptionIndex > 0);
            SplitActiveBadge.Visibility = anySplit ? Visibility.Visible : Visibility.Collapsed;
            AutoSplitBtn.IsEnabled = _auxEndpoint is { IsActive: true } || _auxEndpoint is { IsDisabled: true };
        }
        finally
        {
            _isUpdatingUi = prevUpdating;
        }
    }

    private List<AppEndpointOption> BuildAppRenderOptions()
    {
        var opts = new List<AppEndpointOption>
        {
            new(null, "Default")
        };

        if (_auxEndpoint is { IsActive: true } or { IsDisabled: true })
        {
            opts.Add(new AppEndpointOption(_auxEndpoint.Id, "AUX (3.5mm)"));
        }

        foreach (var bt in _btEndpoints.Where(b => b.IsActive || b.IsDisabled))
        {
            string tag = bt.IsHandsFree ? "HFP" : "BT";
            opts.Add(new AppEndpointOption(bt.Id, $"{tag} • {bt.ShortName}"));
        }

        if (_speakersEndpoint is { IsActive: true })
        {
            opts.Add(new AppEndpointOption(_speakersEndpoint.Id, "Speakers"));
        }

        return opts;
    }
}
