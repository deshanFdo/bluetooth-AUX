using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace AudioSwitch.AudioCore;

internal sealed class AudioRouterService : IMMNotificationClient, IDisposable
{
    private static readonly Guid EventContextGuid = new("D9B8B46E-4D1F-4A2D-9E71-0A3B7D3A2101");

    private readonly IMMDeviceEnumerator _enumerator;
    private readonly PolicyConfigClient _policyConfig = new();
    private readonly object _lock = new();
    private bool _disposed;

    public event Action? AudioStateChanged;

    public AudioRouterService()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public IReadOnlyList<AudioEndpointInfo> GetRenderEndpoints() => GetEndpoints(EDataFlow.Render);

    public IReadOnlyList<AudioEndpointInfo> GetCaptureEndpoints() => GetEndpoints(EDataFlow.Capture);

    private IReadOnlyList<AudioEndpointInfo> GetEndpoints(EDataFlow dataFlow)
    {
        var list = new List<AudioEndpointInfo>();

        string? defaultPrimaryId = GetDefaultEndpointId(dataFlow, ERole.Multimedia)
                                   ?? GetDefaultEndpointId(dataFlow, ERole.Console);
        string? defaultCommsId = GetDefaultEndpointId(dataFlow, ERole.Communications);

        int hr = _enumerator.EnumAudioEndpoints(
            dataFlow,
            DeviceState.Active | DeviceState.Disabled | DeviceState.Unplugged,
            out IMMDeviceCollection? collection);

        if (hr != 0 || collection == null)
        {
            return list;
        }

        try
        {
            collection.GetCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                if (collection.Item(i, out IMMDevice? dev) != 0 || dev == null)
                {
                    continue;
                }

                try
                {
                    var info = ReadEndpointInfo(dev, dataFlow, defaultPrimaryId, defaultCommsId);
                    if (info != null)
                    {
                        list.Add(info);
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

        return list;
    }

    public (bool Success, int HResult, string Message) SwitchToEndpoint(
        string targetDeviceId,
        IEnumerable<string>? endpointsToMute = null,
        bool unmuteTarget = true)
    {
        int hr = _policyConfig.SetDefaultEndpoint(targetDeviceId);
        if (hr != 0)
        {
            return (false, hr, $"Failed to switch default audio device (0x{hr:X8}).");
        }

        if (unmuteTarget)
        {
            SetMute(targetDeviceId, false);
        }

        if (endpointsToMute != null)
        {
            foreach (var id in endpointsToMute)
            {
                if (!string.Equals(id, targetDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    SetMute(id, true);
                }
            }
        }

        return (true, 0, "Switched default audio device.");
    }

    public (bool Success, int HResult, string Message) SetEndpointVisibility(string deviceId, bool visible)
    {
        int hr = _policyConfig.SetEndpointVisibility(deviceId, visible);
        if (hr == 0)
        {
            return (true, 0, visible ? "Endpoint enabled in Windows." : "Endpoint disabled in Windows.");
        }

        if (hr == unchecked((int)0x80070005))
        {
            return (false, hr, "Windows requires Administrator rights to hard-disable an endpoint. Using mute + route instead.");
        }

        return (false, hr, $"SetEndpointVisibility failed (0x{hr:X8}).");
    }

    public bool SetMute(string deviceId, bool mute)
    {
        return WithEndpointVolume(deviceId, vol =>
        {
            var ctx = EventContextGuid;
            return vol.SetMute(mute, ref ctx) == 0;
        });
    }

    public bool SetVolume(string deviceId, int percent)
    {
        float scalar = Math.Clamp(percent, 0, 100) / 100f;
        return WithEndpointVolume(deviceId, vol =>
        {
            var ctx = EventContextGuid;
            return vol.SetMasterVolumeLevelScalar(scalar, ref ctx) == 0;
        });
    }

    public float GetPeakLevel(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return 0f;

        try
        {
            if (_enumerator.GetDevice(deviceId, out IMMDevice? dev) != 0 || dev == null)
            {
                return 0f;
            }

            try
            {
                Guid iid = ComIds.IID_IAudioMeterInformation;
                if (dev.Activate(ref iid, ComIds.CLSCTX_ALL, IntPtr.Zero, out object? obj) != 0 || obj == null)
                {
                    return 0f;
                }

                try
                {
                    var meter = (IAudioMeterInformation)obj;
                    return meter.GetPeakValue(out float peak) == 0 ? Math.Clamp(peak, 0f, 1f) : 0f;
                }
                finally
                {
                    Marshal.ReleaseComObject(obj);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(dev);
            }
        }
        catch
        {
            return 0f;
        }
    }

    public async Task<(bool Success, string Message)> TryConnectBluetoothAudioAsync(string deviceNameHint)
    {
        try
        {
            var selector = AudioPlaybackConnection.GetDeviceSelector();
            var devices = await DeviceInformation.FindAllAsync(selector);
            if (devices.Count == 0)
            {
                return (false, "No Bluetooth audio sink devices found.");
            }

            DeviceInformation? match = devices.FirstOrDefault(d =>
                !string.IsNullOrWhiteSpace(deviceNameHint) &&
                (d.Name.Contains(deviceNameHint, StringComparison.OrdinalIgnoreCase) ||
                 deviceNameHint.Contains(d.Name, StringComparison.OrdinalIgnoreCase)))
                ?? devices.FirstOrDefault();

            if (match == null)
            {
                return (false, "Matching Bluetooth device not found.");
            }

            using var connection = AudioPlaybackConnection.TryCreateFromId(match.Id);
            if (connection == null)
            {
                return (false, $"Could not initialize audio connection for {match.Name}.");
            }

            await connection.StartAsync();
            var result = await connection.OpenAsync();
            if (result.Status == AudioPlaybackConnectionOpenResultStatus.Success)
            {
                return (true, $"Connected to {match.Name}.");
            }

            return (false, $"Could not connect to {match.Name} ({result.Status}). Make sure the headset is powered on.");
        }
        catch (Exception ex)
        {
            return (false, $"Bluetooth connect failed: {ex.Message}");
        }
    }

    private string? GetDefaultEndpointId(EDataFlow dataFlow, ERole role)
    {
        if (_enumerator.GetDefaultAudioEndpoint(dataFlow, role, out IMMDevice? dev) != 0 || dev == null)
        {
            return null;
        }

        try
        {
            return dev.GetId(out string id) == 0 ? id : null;
        }
        finally
        {
            Marshal.ReleaseComObject(dev);
        }
    }

    private AudioEndpointInfo? ReadEndpointInfo(
        IMMDevice dev,
        EDataFlow dataFlow,
        string? defaultPrimaryId,
        string? defaultCommsId)
    {
        if (dev.GetId(out string id) != 0 || string.IsNullOrEmpty(id))
        {
            return null;
        }

        dev.GetState(out DeviceState state);

        string friendlyName = id;
        string deviceDesc = string.Empty;
        string adapterName = string.Empty;
        string enumeratorName = string.Empty;
        EndpointFormFactor formFactor = EndpointFormFactor.UnknownFormFactor;

        if (dev.OpenPropertyStore(ComIds.STGM_READ, out IPropertyStore? props) == 0 && props != null)
        {
            try
            {
                friendlyName = GetStringProperty(props, PropertyKey.FriendlyName) ?? id;
                deviceDesc = GetStringProperty(props, PropertyKey.DeviceDesc) ?? friendlyName;
                adapterName = GetStringProperty(props, PropertyKey.InterfaceFriendlyName) ?? string.Empty;
                enumeratorName = GetStringProperty(props, PropertyKey.EnumeratorName) ?? string.Empty;

                uint? ff = GetUIntProperty(props, PropertyKey.AudioEndpointFormFactor);
                if (ff.HasValue && Enum.IsDefined(typeof(EndpointFormFactor), ff.Value))
                {
                    formFactor = (EndpointFormFactor)ff.Value;
                }
            }
            finally
            {
                Marshal.ReleaseComObject(props);
            }
        }

        bool isHandsFree = enumeratorName.Equals("BTHHFENUM", StringComparison.OrdinalIgnoreCase)
                           || friendlyName.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase);

        bool isVirtual = enumeratorName.Equals("ROOT", StringComparison.OrdinalIgnoreCase) ||
                         adapterName.Contains("Steam", StringComparison.OrdinalIgnoreCase) ||
                         adapterName.Contains("DroidCam", StringComparison.OrdinalIgnoreCase) ||
                         adapterName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
                         friendlyName.Contains("Steam Streaming", StringComparison.OrdinalIgnoreCase);

        AudioCategory category = ClassifyEndpoint(dataFlow, friendlyName, adapterName, enumeratorName, formFactor, isVirtual);

        int volumePercent = 0;
        bool isMuted = false;

        if (state == DeviceState.Active)
        {
            WithEndpointVolume(dev, vol =>
            {
                if (vol.GetMasterVolumeLevelScalar(out float scalar) == 0)
                {
                    volumePercent = (int)Math.Round(scalar * 100f);
                }
                if (vol.GetMute(out bool muted) == 0)
                {
                    isMuted = muted;
                }
                return true;
            });
        }

        string shortName = !string.IsNullOrWhiteSpace(adapterName) &&
                           !friendlyName.Contains(adapterName, StringComparison.OrdinalIgnoreCase)
            ? friendlyName
            : deviceDesc;

        if (string.IsNullOrWhiteSpace(shortName))
        {
            shortName = friendlyName;
        }

        return new AudioEndpointInfo
        {
            Id = id,
            DataFlow = dataFlow,
            FriendlyName = friendlyName,
            ShortName = shortName,
            AdapterName = adapterName,
            EnumeratorName = enumeratorName,
            State = state,
            FormFactor = formFactor,
            Category = category,
            IsHandsFree = isHandsFree,
            IsVirtual = isVirtual,
            IsDefaultPlayback = string.Equals(id, defaultPrimaryId, StringComparison.OrdinalIgnoreCase),
            IsDefaultCommunications = string.Equals(id, defaultCommsId, StringComparison.OrdinalIgnoreCase),
            VolumePercent = volumePercent,
            IsMuted = isMuted
        };
    }

    private static AudioCategory ClassifyEndpoint(
        EDataFlow dataFlow,
        string friendlyName,
        string adapterName,
        string enumeratorName,
        EndpointFormFactor formFactor,
        bool isVirtual)
    {
        if (enumeratorName.StartsWith("BTH", StringComparison.OrdinalIgnoreCase) ||
            friendlyName.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase) ||
            friendlyName.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) ||
            adapterName.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
        {
            return AudioCategory.Bluetooth;
        }

        if (isVirtual || formFactor == EndpointFormFactor.DigitalAudioDisplayDevice || formFactor == EndpointFormFactor.Spdif)
        {
            return AudioCategory.Other;
        }

        if (dataFlow == EDataFlow.Capture)
        {
            if (friendlyName.Contains("Microphone Array", StringComparison.OrdinalIgnoreCase) ||
                friendlyName.Contains("Internal", StringComparison.OrdinalIgnoreCase))
            {
                return AudioCategory.BuiltInMic;
            }

            if (formFactor is EndpointFormFactor.Microphone or EndpointFormFactor.Headset or EndpointFormFactor.LineLevel)
            {
                return AudioCategory.AuxWired;
            }

            return AudioCategory.Other;
        }

        if (formFactor is EndpointFormFactor.Headphones or EndpointFormFactor.Headset or EndpointFormFactor.LineLevel)
        {
            return AudioCategory.AuxWired;
        }

        if (friendlyName.Contains("Headphone", StringComparison.OrdinalIgnoreCase) ||
            friendlyName.Contains("AUX", StringComparison.OrdinalIgnoreCase) ||
            friendlyName.Contains("Line Out", StringComparison.OrdinalIgnoreCase))
        {
            return AudioCategory.AuxWired;
        }

        if (formFactor == EndpointFormFactor.Speakers ||
            friendlyName.Contains("Speakers", StringComparison.OrdinalIgnoreCase))
        {
            return AudioCategory.BuiltInSpeakers;
        }

        return AudioCategory.Other;
    }

    private bool WithEndpointVolume(string deviceId, Func<IAudioEndpointVolume, bool> action)
    {
        if (_enumerator.GetDevice(deviceId, out IMMDevice? dev) != 0 || dev == null)
        {
            return false;
        }

        try
        {
            return WithEndpointVolume(dev, action);
        }
        finally
        {
            Marshal.ReleaseComObject(dev);
        }
    }

    private static bool WithEndpointVolume(IMMDevice dev, Func<IAudioEndpointVolume, bool> action)
    {
        Guid iid = ComIds.IID_IAudioEndpointVolume;
        if (dev.Activate(ref iid, ComIds.CLSCTX_ALL, IntPtr.Zero, out object? obj) != 0 || obj == null)
        {
            return false;
        }

        try
        {
            return action((IAudioEndpointVolume)obj);
        }
        finally
        {
            Marshal.ReleaseComObject(obj);
        }
    }

    private static string? GetStringProperty(IPropertyStore store, PropertyKey key)
    {
        if (store.GetValue(ref key, out PropVariant pv) != 0)
        {
            return null;
        }

        try
        {
            return pv.GetStringValue();
        }
        finally
        {
            PropVariant.PropVariantClear(ref pv);
        }
    }

    private static uint? GetUIntProperty(IPropertyStore store, PropertyKey key)
    {
        if (store.GetValue(ref key, out PropVariant pv) != 0)
        {
            return null;
        }

        try
        {
            return pv.GetUIntValue();
        }
        finally
        {
            PropVariant.PropVariantClear(ref pv);
        }
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => NotifyChanged();
    public void OnDeviceAdded(string pwstrDeviceId) => NotifyChanged();
    public void OnDeviceRemoved(string deviceId) => NotifyChanged();
    public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId) => NotifyChanged();
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

    private void NotifyChanged()
    {
        lock (_lock)
        {
            if (_disposed) return;
        }
        AudioStateChanged?.Invoke();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(this);
        }
        catch
        {
            // Ignore shutdown COM races
        }

        Marshal.ReleaseComObject(_enumerator);
    }
}

/// <summary>
/// Samples peak audio levels on a background MTA thread with cached IAudioMeterInformation
/// so the WPF UI animation thread never blocks on COM activation or device queries.
/// </summary>
internal sealed class BackgroundPeakSampler : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ManualResetEventSlim _wakeGate = new(false);
    private readonly Task _task;

    private string? _targetOutId;
    private string? _targetMicId;
    private volatile bool _active;
    private volatile float _outPeak;
    private volatile float _micPeak;

    public float LatestOutPeak => _outPeak;
    public float LatestMicPeak => _micPeak;

    public BackgroundPeakSampler()
    {
        _task = Task.Factory.StartNew(
            SampleLoop,
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public void SetTargets(string? outputDeviceId, string? micDeviceId, bool enabled)
    {
        Volatile.Write(ref _targetOutId, outputDeviceId);
        Volatile.Write(ref _targetMicId, micDeviceId);
        _active = enabled;
        if (enabled)
        {
            _wakeGate.Set();
        }
        else
        {
            _wakeGate.Reset();
            _outPeak = 0f;
            _micPeak = 0f;
        }
    }

    private void SampleLoop()
    {
        IMMDeviceEnumerator? enumerator = null;
        string? cachedOutId = null;
        IAudioMeterInformation? cachedOutMeter = null;
        string? cachedMicId = null;
        IAudioMeterInformation? cachedMicMeter = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                if (!_active)
                {
                    // Park completely with 0% CPU until island expands
                    _wakeGate.Wait(token);
                    continue;
                }

                string? outId = Volatile.Read(ref _targetOutId);
                string? micId = Volatile.Read(ref _targetMicId);

                _outPeak = ReadPeakCached(enumerator, outId, ref cachedOutId, ref cachedOutMeter);
                _micPeak = ReadPeakCached(enumerator, micId, ref cachedMicId, ref cachedMicMeter);

                Thread.Sleep(24);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch
        {
            // Ignore shutdown COM races
        }
        finally
        {
            ReleaseCached(ref cachedOutId, ref cachedOutMeter);
            ReleaseCached(ref cachedMicId, ref cachedMicMeter);
            if (enumerator != null)
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
    }

    private static float ReadPeakCached(
        IMMDeviceEnumerator enumerator,
        string? targetId,
        ref string? cachedId,
        ref IAudioMeterInformation? cachedMeter)
    {
        if (string.IsNullOrEmpty(targetId))
        {
            ReleaseCached(ref cachedId, ref cachedMeter);
            return 0f;
        }

        if (!string.Equals(targetId, cachedId, StringComparison.OrdinalIgnoreCase) || cachedMeter == null)
        {
            ReleaseCached(ref cachedId, ref cachedMeter);
            if (enumerator.GetDevice(targetId, out IMMDevice? dev) == 0 && dev != null)
            {
                try
                {
                    Guid iid = ComIds.IID_IAudioMeterInformation;
                    if (dev.Activate(ref iid, ComIds.CLSCTX_ALL, IntPtr.Zero, out object? obj) == 0 && obj != null)
                    {
                        cachedMeter = (IAudioMeterInformation)obj;
                        cachedId = targetId;
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(dev);
                }
            }
        }

        if (cachedMeter == null) return 0f;

        try
        {
            return cachedMeter.GetPeakValue(out float peak) == 0 ? Math.Clamp(peak, 0f, 1f) : 0f;
        }
        catch
        {
            ReleaseCached(ref cachedId, ref cachedMeter);
            return 0f;
        }
    }

    private static void ReleaseCached(ref string? id, ref IAudioMeterInformation? meter)
    {
        id = null;
        if (meter != null)
        {
            try { Marshal.ReleaseComObject(meter); } catch { }
            meter = null;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _wakeGate.Set();
        try { _task.Wait(300); } catch { }
        _wakeGate.Dispose();
        _cts.Dispose();
    }
}
