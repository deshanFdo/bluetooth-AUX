using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AudioSwitch.AudioCore;

internal sealed record AppAudioSessionInfo(
    string ProcessName,
    string DisplayName,
    IReadOnlyList<uint> ProcessIds,
    bool IsAudioActive,
    string? RoutedRenderEndpointId,
    string? RoutedCaptureEndpointId);

internal sealed class AppAudioRouterService : IDisposable
{
    private const string AudioPolicyConfigClassName = "Windows.Media.Internal.AudioPolicyConfig";
    private const string SwdPrefix = @"\\?\SWD#MMDEVAPI#";
    private const string RenderInterfaceGuid = "{e6327cad-dcec-4949-ae8a-991e976a79d2}";
    private const string CaptureInterfaceGuid = "{2eef81be-33fa-4800-9670-1cd474972c3f}";

    private static readonly Guid IidAudioPolicyConfigWin11 = new("ab3d4648-e242-459f-b02f-541c70306324");
    private static readonly Guid IidAudioPolicyConfigWin10 = new("2a59116d-6c4f-45e0-a74f-707e3fef9258");

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetPersistedDefaultAudioEndpointDelegate(
        IntPtr instance,
        uint processId,
        EDataFlow flow,
        ERole role,
        IntPtr hstringDeviceId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetPersistedDefaultAudioEndpointDelegate(
        IntPtr instance,
        uint processId,
        EDataFlow flow,
        int roleMask,
        out IntPtr hstringDeviceId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ClearAllPersistedApplicationDefaultEndpointsDelegate(IntPtr instance);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int RoGetActivationFactory(
        IntPtr activatableClassId,
        ref Guid iid,
        out IntPtr factory);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        int length,
        out IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

    private readonly object _sync = new();
    private IntPtr _factoryPtr;
    private string _activeInterfaceName = "Unavailable";
    private SetPersistedDefaultAudioEndpointDelegate? _setPersisted;
    private GetPersistedDefaultAudioEndpointDelegate? _getPersisted;
    private ClearAllPersistedApplicationDefaultEndpointsDelegate? _clearAll;

    // Remember pinned app -> endpoint rules for this run so multi-PID apps / respawned streams stay synced without polling timers.
    private readonly Dictionary<string, (string? RenderId, string? CaptureId)> _appPins = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] KnownMediaProcesses =
    [
        "spotify", "chrome", "msedge", "firefox", "brave", "opera",
        "vlc", "tidal", "foobar2000", "musicbee", "itunes"
    ];

    private static readonly string[] KnownCallProcesses =
    [
        "discord", "ms-teams", "teams", "zoom", "skype"
    ];

    public string ActiveInterfaceName => _activeInterfaceName;

    public static bool IsCallProcess(string procName) =>
        KnownCallProcesses.Any(k => k.Equals(procName, StringComparison.OrdinalIgnoreCase));

    public static bool IsMediaProcess(string procName) =>
        KnownMediaProcesses.Any(k => k.Equals(procName, StringComparison.OrdinalIgnoreCase));

    public bool IsSupported
    {
        get
        {
            EnsureFactory();
            return _factoryPtr != IntPtr.Zero && _setPersisted != null;
        }
    }

    public IReadOnlyCollection<string> GetPinnedRenderEndpointIds()
    {
        lock (_sync)
        {
            return _appPins.Values
                .Where(v => !string.IsNullOrEmpty(v.RenderId))
                .Select(v => v.RenderId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public IReadOnlyCollection<string> GetPinnedCaptureEndpointIds()
    {
        lock (_sync)
        {
            return _appPins.Values
                .Where(v => !string.IsNullOrEmpty(v.CaptureId))
                .Select(v => v.CaptureId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public IReadOnlyList<AppAudioSessionInfo> GetRunningAudioApps()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        var grouped = new Dictionary<string, AppBucket>(StringComparer.OrdinalIgnoreCase);
        int selfPid = Environment.ProcessId;

        try
        {
            ScanSessionsForFlow(enumerator, EDataFlow.Render, selfPid, grouped);
            ScanSessionsForFlow(enumerator, EDataFlow.Capture, selfPid, grouped);
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }

        // Include running media/call apps even if currently paused without an active audio stream.
        foreach (string known in KnownCallProcesses.Concat(KnownMediaProcesses))
        {
            if (grouped.ContainsKey(known)) continue;
            try
            {
                var procs = Process.GetProcessesByName(known);
                if (procs.Length > 0)
                {
                    using var first = procs[0];
                    grouped[known] = new AppBucket(known, PrettyAppName(first, known));
                }
                foreach (var p in procs) p.Dispose();
            }
            catch
            {
                // Ignore transient query failures
            }
        }

        // Also enrich grouped apps with any other live PIDs sharing the same ProcessName (crucial for Discord/Chrome/Spotify multi-process).
        var result = new List<AppAudioSessionInfo>(grouped.Count);
        foreach (var bucket in grouped.Values)
        {
            var allPids = new HashSet<uint>(bucket.ProcessIds);
            try
            {
                foreach (var p in Process.GetProcessesByName(bucket.ProcessName))
                {
                    try
                    {
                        if (p.Id > 0 && p.Id != selfPid)
                        {
                            allPids.Add((uint)p.Id);
                        }
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch
            {
                // Best-effort PID expansion
            }

            string? persistedRender = QueryPersistedForAnyPid(allPids, EDataFlow.Render);
            string? persistedCapture = QueryPersistedForAnyPid(allPids, EDataFlow.Capture);

            result.Add(new AppAudioSessionInfo(
                bucket.ProcessName,
                bucket.DisplayName,
                allPids.OrderBy(x => x).ToArray(),
                bucket.IsActive,
                persistedRender,
                persistedCapture));
        }

        return result
            .OrderByDescending(a => a.IsAudioActive)
            .ThenByDescending(a => !string.IsNullOrEmpty(a.RoutedRenderEndpointId))
            .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public (bool Success, string Message) SetAppRoute(
        string processName,
        EDataFlow flow,
        string? endpointId)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return (false, "Invalid application process name.");
        }

        if (!IsSupported)
        {
            return (false, $"Windows per-app audio policy interface unavailable ({_activeInterfaceName}).");
        }

        var pids = CollectLiveProcessIds(processName);
        if (pids.Count == 0)
        {
            return (false, $"{processName} is not currently running.");
        }

        string? swdId = string.IsNullOrWhiteSpace(endpointId)
            ? null
            : FormatSwdDeviceId(endpointId, flow);

        int appliedCount = 0;
        int lastHr = 0;

        IntPtr hstr = IntPtr.Zero;
        try
        {
            if (!string.IsNullOrEmpty(swdId))
            {
                int createHr = WindowsCreateString(swdId, swdId.Length, out hstr);
                if (createHr != 0)
                {
                    return (false, $"WindowsCreateString failed (0x{createHr:X8}).");
                }
            }

            foreach (uint pid in pids)
            {
                int hr0 = _setPersisted!(_factoryPtr, pid, flow, ERole.Console, hstr);
                int hr1 = _setPersisted!(_factoryPtr, pid, flow, ERole.Multimedia, hstr);
                int hr2 = _setPersisted!(_factoryPtr, pid, flow, ERole.Communications, hstr);
                if (hr0 == 0 || hr1 == 0 || hr2 == 0)
                {
                    appliedCount++;
                }
                else
                {
                    lastHr = hr1 != 0 ? hr1 : hr0;
                }
            }
        }
        finally
        {
            if (hstr != IntPtr.Zero)
            {
                WindowsDeleteString(hstr);
            }
        }

        lock (_sync)
        {
            _appPins.TryGetValue(processName, out var existing);
            var updated = flow == EDataFlow.Render
                ? (endpointId, existing.CaptureId)
                : (existing.RenderId, endpointId);
            if (string.IsNullOrEmpty(updated.Item1) && string.IsNullOrEmpty(updated.Item2))
            {
                _appPins.Remove(processName);
            }
            else
            {
                _appPins[processName] = updated;
            }
        }

        if (appliedCount == 0)
        {
            return (false, $"Could not apply route for {processName} (0x{lastHr:X8}).");
        }

        return (true, string.IsNullOrEmpty(endpointId)
            ? $"Reset {processName} to Windows Default ({appliedCount} process{(appliedCount == 1 ? "" : "es")})."
            : $"Routed {processName} ({appliedCount} process{(appliedCount == 1 ? "" : "es")}).");
    }

    public (bool Match, bool Cleared, int SetHr, string? ReadBack) ProbePidRoundTrip(uint pid, string endpointId)
    {
        if (!IsSupported) return (false, false, -1, null);

        string swdId = FormatSwdDeviceId(endpointId, EDataFlow.Render);
        IntPtr hstr = IntPtr.Zero;
        int setHr = -1;
        try
        {
            if (WindowsCreateString(swdId, swdId.Length, out hstr) == 0)
            {
                setHr = _setPersisted!(_factoryPtr, pid, EDataFlow.Render, ERole.Multimedia, hstr);
            }
        }
        finally
        {
            if (hstr != IntPtr.Zero) WindowsDeleteString(hstr);
        }

        string? readBack = GetPersistedEndpointId(pid, EDataFlow.Render);
        _setPersisted!(_factoryPtr, pid, EDataFlow.Render, ERole.Multimedia, IntPtr.Zero);
        string? afterClear = GetPersistedEndpointId(pid, EDataFlow.Render);

        return (
            string.Equals(readBack, endpointId, StringComparison.OrdinalIgnoreCase),
            afterClear == null,
            setHr,
            readBack);
    }

    public bool ResetAllPersistedAppRoutes()
    {
        if (!IsSupported || _clearAll == null) return false;
        int hr = _clearAll(_factoryPtr);
        lock (_sync)
        {
            _appPins.Clear();
        }
        return hr == 0;
    }

    public void ReapplyPinnedRoutesIfPresent(IEnumerable<AppAudioSessionInfo> currentApps)
    {
        KeyValuePair<string, (string? RenderId, string? CaptureId)>[] snapshot;
        lock (_sync)
        {
            if (_appPins.Count == 0) return;
            snapshot = _appPins.ToArray();
        }

        var byProc = currentApps.ToDictionary(a => a.ProcessName, StringComparer.OrdinalIgnoreCase);
        foreach (var (proc, rule) in snapshot)
        {
            if (!byProc.TryGetValue(proc, out var app)) continue;
            if (!string.IsNullOrEmpty(rule.RenderId) &&
                !string.Equals(app.RoutedRenderEndpointId, rule.RenderId, StringComparison.OrdinalIgnoreCase))
            {
                SetAppRoute(proc, EDataFlow.Render, rule.RenderId);
            }
            if (!string.IsNullOrEmpty(rule.CaptureId) &&
                !string.Equals(app.RoutedCaptureEndpointId, rule.CaptureId, StringComparison.OrdinalIgnoreCase))
            {
                SetAppRoute(proc, EDataFlow.Capture, rule.CaptureId);
            }
        }
    }

    public string? GetPersistedEndpointId(uint processId, EDataFlow flow)
    {
        if (!IsSupported || _getPersisted == null) return null;

        // Role mask 0x7 checks Console (1) | Multimedia (2) | Communications (4); role 1 checks Multimedia.
        foreach (int mask in new[] { 0x2, 0x4, 0x1, 0x7 })
        {
            IntPtr hstr = IntPtr.Zero;
            try
            {
                int hr = _getPersisted(_factoryPtr, processId, flow, mask, out hstr);
                if (hr == 0 && hstr != IntPtr.Zero)
                {
                    IntPtr buf = WindowsGetStringRawBuffer(hstr, out uint len);
                    if (buf != IntPtr.Zero && len > 0)
                    {
                        string raw = Marshal.PtrToStringUni(buf, (int)len);
                        string? parsed = ExtractRawEndpointId(raw);
                        if (!string.IsNullOrEmpty(parsed))
                        {
                            return parsed;
                        }
                    }
                }
            }
            finally
            {
                if (hstr != IntPtr.Zero)
                {
                    WindowsDeleteString(hstr);
                }
            }
        }

        return null;
    }

    public static string FormatSwdDeviceId(string endpointId, EDataFlow flow)
    {
        if (endpointId.StartsWith(SwdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return endpointId;
        }

        string iface = flow == EDataFlow.Capture ? CaptureInterfaceGuid : RenderInterfaceGuid;
        return $"{SwdPrefix}{endpointId}#{iface}";
    }

    public static string? ExtractRawEndpointId(string? swdOrRaw)
    {
        if (string.IsNullOrWhiteSpace(swdOrRaw)) return null;
        if (!swdOrRaw.StartsWith(SwdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return swdOrRaw;
        }

        string remainder = swdOrRaw.Substring(SwdPrefix.Length);
        int hashIdx = remainder.IndexOf('#');
        return hashIdx > 0 ? remainder.Substring(0, hashIdx) : remainder;
    }

    private string? QueryPersistedForAnyPid(IEnumerable<uint> pids, EDataFlow flow)
    {
        foreach (uint pid in pids)
        {
            string? id = GetPersistedEndpointId(pid, flow);
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }
        }
        return null;
    }

    private static List<uint> CollectLiveProcessIds(string processName)
    {
        var pids = new List<uint>();
        int selfPid = Environment.ProcessId;
        try
        {
            foreach (var p in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (p.Id > 0 && p.Id != selfPid)
                    {
                        pids.Add((uint)p.Id);
                    }
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch
        {
            // Ignore transient access failures
        }
        return pids;
    }

    private static void ScanSessionsForFlow(
        IMMDeviceEnumerator enumerator,
        EDataFlow flow,
        int selfPid,
        Dictionary<string, AppBucket> grouped)
    {
        if (enumerator.EnumAudioEndpoints(flow, DeviceState.Active, out IMMDeviceCollection? collection) != 0 || collection == null)
        {
            return;
        }

        try
        {
            collection.GetCount(out uint devCount);
            for (uint d = 0; d < devCount; d++)
            {
                if (collection.Item(d, out IMMDevice? dev) != 0 || dev == null) continue;
                try
                {
                    Guid mgrIid = ComIds.IID_IAudioSessionManager2;
                    if (dev.Activate(ref mgrIid, ComIds.CLSCTX_ALL, IntPtr.Zero, out object? mgrObj) != 0 || mgrObj == null)
                    {
                        continue;
                    }

                    try
                    {
                        var mgr = (IAudioSessionManager2)mgrObj;
                        if (mgr.GetSessionEnumerator(out IAudioSessionEnumerator? sessionEnum) != 0 || sessionEnum == null)
                        {
                            continue;
                        }

                        try
                        {
                            sessionEnum.GetCount(out int count);
                            for (int i = 0; i < count; i++)
                            {
                                if (sessionEnum.GetSession(i, out IAudioSessionControl? ctrl) != 0 || ctrl == null)
                                {
                                    continue;
                                }

                                try
                                {
                                    if (ctrl is not IAudioSessionControl2 ctrl2) continue;
                                    if (ctrl2.IsSystemSoundsSession() == 0) continue;
                                    if (ctrl2.GetProcessId(out uint pid) != 0 || pid == 0 || pid == (uint)selfPid) continue;

                                    ctrl2.GetState(out AudioSessionState state);
                                    if (state == AudioSessionState.Expired) continue;

                                    var (procName, friendly) = ResolveProcessNames(pid, ctrl2);
                                    if (string.IsNullOrWhiteSpace(procName)) continue;
                                    if (IsIgnoredSystemAudioProcess(procName)) continue;

                                    if (!grouped.TryGetValue(procName, out var bucket))
                                    {
                                        bucket = new AppBucket(procName, friendly);
                                        grouped[procName] = bucket;
                                    }

                                    bucket.ProcessIds.Add(pid);
                                    if (state == AudioSessionState.Active)
                                    {
                                        bucket.IsActive = true;
                                    }
                                    if (!string.IsNullOrWhiteSpace(friendly) &&
                                        bucket.DisplayName.Equals(bucket.ProcessName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        bucket.DisplayName = friendly;
                                    }
                                }
                                finally
                                {
                                    Marshal.ReleaseComObject(ctrl);
                                }
                            }
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(sessionEnum);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(mgrObj);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(dev);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(collection);
        }
    }

    private static (string ProcessName, string DisplayName) ResolveProcessNames(uint pid, IAudioSessionControl2 ctrl2)
    {
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            string procName = proc.ProcessName;
            string display = PrettyAppName(proc, procName);
            if (string.Equals(display, procName, StringComparison.OrdinalIgnoreCase))
            {
                if (ctrl2.GetDisplayName(out string? sessionDisplay) == 0 &&
                    !string.IsNullOrWhiteSpace(sessionDisplay) &&
                    !sessionDisplay.StartsWith("@", StringComparison.Ordinal))
                {
                    display = sessionDisplay.Trim();
                }
            }
            return (procName, display);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private static string PrettyAppName(Process proc, string procName)
    {
        return procName.ToLowerInvariant() switch
        {
            "discord" => "Discord",
            "spotify" => "Spotify",
            "chrome" => "Google Chrome",
            "msedge" => "Microsoft Edge",
            "firefox" => "Firefox",
            "brave" => "Brave",
            "opera" => "Opera",
            "ms-teams" or "teams" => "Microsoft Teams",
            "zoom" => "Zoom",
            "obs64" => "OBS Studio",
            "vlc" => "VLC",
            "foobar2000" => "foobar2000",
            "musicbee" => "MusicBee",
            "tidal" => "TIDAL",
            "itunes" => "Apple Music / iTunes",
            _ => ReadFriendlyFileDescription(proc, procName)
        };
    }

    private static string ReadFriendlyFileDescription(Process proc, string fallback)
    {
        try
        {
            string? path = proc.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(info.FileDescription) &&
                    info.FileDescription.Length <= 28)
                {
                    return info.FileDescription.Trim();
                }

                if (!string.IsNullOrWhiteSpace(info.ProductName) &&
                    info.ProductName.Length <= 28)
                {
                    return info.ProductName.Trim();
                }

                return Path.GetFileNameWithoutExtension(path);
            }
        }
        catch
        {
            // Access denied on protected process module path; fallback to ProcessName.
        }

        return char.ToUpperInvariant(fallback[0]) + fallback.Substring(1);
    }

    private static bool IsIgnoredSystemAudioProcess(string procName) =>
        procName.Equals("svchost", StringComparison.OrdinalIgnoreCase) ||
        procName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase) ||
        procName.Equals("SearchHost", StringComparison.OrdinalIgnoreCase) ||
        procName.Equals("audiodg", StringComparison.OrdinalIgnoreCase);

    private void EnsureFactory()
    {
        if (_factoryPtr != IntPtr.Zero) return;

        lock (_sync)
        {
            if (_factoryPtr != IntPtr.Zero) return;

            IntPtr classIdHstr = IntPtr.Zero;
            try
            {
                int hrStr = WindowsCreateString(AudioPolicyConfigClassName, AudioPolicyConfigClassName.Length, out classIdHstr);
                if (hrStr != 0 || classIdHstr == IntPtr.Zero)
                {
                    _activeInterfaceName = $"WindowsCreateString failed (0x{hrStr:X8})";
                    return;
                }

                Guid win11Iid = IidAudioPolicyConfigWin11;
                int hr = RoGetActivationFactory(classIdHstr, ref win11Iid, out IntPtr ptr);
                if (hr == 0 && ptr != IntPtr.Zero)
                {
                    _factoryPtr = ptr;
                    _activeInterfaceName = "IAudioPolicyConfigFactory (Win11 21H2+)";
                }
                else
                {
                    Guid win10Iid = IidAudioPolicyConfigWin10;
                    hr = RoGetActivationFactory(classIdHstr, ref win10Iid, out ptr);
                    if (hr == 0 && ptr != IntPtr.Zero)
                    {
                        _factoryPtr = ptr;
                        _activeInterfaceName = "IAudioPolicyConfigFactory (Win10 RS4+)";
                    }
                    else
                    {
                        _activeInterfaceName = $"RoGetActivationFactory failed (0x{hr:X8})";
                        return;
                    }
                }

                // Read vtable slots: IUnknown (0..2), IInspectable (3..5), IAudioPolicyConfigFactory:
                // 25 = SetPersistedDefaultAudioEndpoint
                // 26 = GetPersistedDefaultAudioEndpoint
                // 27 = ClearAllPersistedApplicationDefaultEndpoints
                IntPtr vtable = Marshal.ReadIntPtr(_factoryPtr);
                _setPersisted = GetVtableDelegate<SetPersistedDefaultAudioEndpointDelegate>(vtable, 25);
                _getPersisted = GetVtableDelegate<GetPersistedDefaultAudioEndpointDelegate>(vtable, 26);
                _clearAll = GetVtableDelegate<ClearAllPersistedApplicationDefaultEndpointsDelegate>(vtable, 27);
            }
            catch (Exception ex)
            {
                _activeInterfaceName = $"Init error: {ex.Message}";
                ReleaseFactory();
            }
            finally
            {
                if (classIdHstr != IntPtr.Zero)
                {
                    WindowsDeleteString(classIdHstr);
                }
            }
        }
    }

    private static TDelegate GetVtableDelegate<TDelegate>(IntPtr vtable, int slotIndex) where TDelegate : Delegate
    {
        IntPtr fnPtr = Marshal.ReadIntPtr(vtable, slotIndex * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<TDelegate>(fnPtr);
    }

    private void ReleaseFactory()
    {
        if (_factoryPtr != IntPtr.Zero)
        {
            Marshal.Release(_factoryPtr);
            _factoryPtr = IntPtr.Zero;
        }
        _setPersisted = null;
        _getPersisted = null;
        _clearAll = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            ReleaseFactory();
        }
    }

    private sealed class AppBucket
    {
        public AppBucket(string processName, string displayName)
        {
            ProcessName = processName;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? processName : displayName;
        }

        public string ProcessName { get; }
        public string DisplayName { get; set; }
        public HashSet<uint> ProcessIds { get; } = new();
        public bool IsActive { get; set; }
    }
}
