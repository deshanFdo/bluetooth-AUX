using System.Runtime.InteropServices;

namespace AudioSwitch.AudioCore;

/// <summary>
/// Lightweight user-mode WASAPI loopback mirror that duplicates audio from a primary
/// render endpoint to one or more secondary active render endpoints, with a live
/// adjustable sync delay (in ms) to align wired outputs with Bluetooth A2DP latency.
/// </summary>
internal sealed class AudioMirrorService : IDisposable
{
    private const int AudClntShareModeShared = 0;
    private const uint AudClntStreamFlagsLoopback = 0x00020000;
    private const uint AudClntStreamFlagsAutoConvertPcm = 0x80000000;
    private const uint AudClntStreamFlagsSrcDefaultQuality = 0x08000000;
    private const uint AudClntBufferFlagsSilent = 0x00000002;
    private const long ReftimesPerMillisecond = 10_000;

    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _workerTask;
    private volatile int _syncDelayMs;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _workerTask != null && !_workerTask.IsCompleted;
            }
        }
    }

    public int SyncDelayMs
    {
        get => _syncDelayMs;
        set => _syncDelayMs = Math.Clamp(value, 0, 400);
    }

    public string? PrimaryDeviceId { get; private set; }
    public IReadOnlyList<string> SecondaryDeviceIds { get; private set; } = Array.Empty<string>();

    public void StartMirroring(string primaryDeviceId, IReadOnlyList<string> secondaryDeviceIds, int initialDelayMs = 0)
    {
        StopMirroring();

        var targets = secondaryDeviceIds
            .Where(id => !string.IsNullOrWhiteSpace(id) &&
                         !string.Equals(id, primaryDeviceId, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
        {
            return;
        }

        _syncDelayMs = Math.Clamp(initialDelayMs, 0, 400);

        lock (_lock)
        {
            PrimaryDeviceId = primaryDeviceId;
            SecondaryDeviceIds = targets;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _workerTask = Task.Factory.StartNew(
                () => RunMirrorLoop(primaryDeviceId, targets, token),
                token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }

    public void StopMirroring()
    {
        Task? toWait = null;
        lock (_lock)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                toWait = _workerTask;
                _cts.Dispose();
                _cts = null;
                _workerTask = null;
            }
            PrimaryDeviceId = null;
            SecondaryDeviceIds = Array.Empty<string>();
        }

        if (toWait != null)
        {
            try
            {
                toWait.Wait(600);
            }
            catch
            {
                // Ignore cancellation / shutdown waits
            }
        }
    }

    private void RunMirrorLoop(string primaryId, List<string> secondaryIds, CancellationToken token)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? primaryDev = null;
        IAudioClient? captureAudioClient = null;
        IAudioCaptureClient? captureClient = null;
        IntPtr mixFormatPtr = IntPtr.Zero;

        var renderTargets = new List<(IMMDevice Device, IAudioClient Client, IAudioRenderClient Render, uint BufferFrames)>();

        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            if (enumerator.GetDevice(primaryId, out primaryDev) != 0 || primaryDev == null)
            {
                return;
            }

            Guid iidAudioClient = ComIds.IID_IAudioClient;
            if (primaryDev.Activate(ref iidAudioClient, ComIds.CLSCTX_ALL, IntPtr.Zero, out object? capObj) != 0 || capObj == null)
            {
                return;
            }
            captureAudioClient = (IAudioClient)capObj;

            if (captureAudioClient.GetMixFormat(out mixFormatPtr) != 0 || mixFormatPtr == IntPtr.Zero)
            {
                return;
            }

            var mixFmt = Marshal.PtrToStructure<WaveFormatEx>(mixFormatPtr);
            int blockAlign = Math.Max(1, (int)mixFmt.nBlockAlign);
            uint sampleRate = Math.Max(8000, mixFmt.nSamplesPerSec);

            Guid sessionGuid = Guid.Empty;
            int hrCapInit = captureAudioClient.Initialize(
                AudClntShareModeShared,
                AudClntStreamFlagsLoopback,
                30 * ReftimesPerMillisecond,
                0,
                mixFormatPtr,
                ref sessionGuid);

            if (hrCapInit != 0)
            {
                return;
            }

            Guid iidCaptureClient = ComIds.IID_IAudioCaptureClient;
            if (captureAudioClient.GetService(ref iidCaptureClient, out object? capSvc) != 0 || capSvc == null)
            {
                return;
            }
            captureClient = (IAudioCaptureClient)capSvc;

            foreach (var targetId in secondaryIds)
            {
                if (enumerator.GetDevice(targetId, out IMMDevice? secDev) != 0 || secDev == null)
                {
                    continue;
                }

                if (secDev.Activate(ref iidAudioClient, ComIds.CLSCTX_ALL, IntPtr.Zero, out object? renObj) != 0 || renObj == null)
                {
                    Marshal.ReleaseComObject(secDev);
                    continue;
                }

                var renAudioClient = (IAudioClient)renObj;
                Guid secSession = Guid.Empty;
                uint flags = AudClntStreamFlagsAutoConvertPcm | AudClntStreamFlagsSrcDefaultQuality;
                int hrRenInit = renAudioClient.Initialize(
                    AudClntShareModeShared,
                    flags,
                    40 * ReftimesPerMillisecond,
                    0,
                    mixFormatPtr,
                    ref secSession);

                if (hrRenInit != 0)
                {
                    Marshal.ReleaseComObject(renAudioClient);
                    Marshal.ReleaseComObject(secDev);
                    continue;
                }

                renAudioClient.GetBufferSize(out uint bufFrames);
                Guid iidRenderClient = ComIds.IID_IAudioRenderClient;
                if (renAudioClient.GetService(ref iidRenderClient, out object? renSvc) != 0 || renSvc == null)
                {
                    Marshal.ReleaseComObject(renAudioClient);
                    Marshal.ReleaseComObject(secDev);
                    continue;
                }

                var renClient = (IAudioRenderClient)renSvc;
                if (renAudioClient.Start() != 0)
                {
                    Marshal.ReleaseComObject(renClient);
                    Marshal.ReleaseComObject(renAudioClient);
                    Marshal.ReleaseComObject(secDev);
                    continue;
                }

                renderTargets.Add((secDev, renAudioClient, renClient, bufFrames));
            }

            if (renderTargets.Count == 0)
            {
                return;
            }

            if (captureAudioClient.Start() != 0)
            {
                return;
            }

            // Circular delay ring buffer (supports up to 450ms at any sample rate)
            int maxDelayFrames = (int)(sampleRate * 0.5);
            var delayRing = new byte[maxDelayFrames * blockAlign];
            int ringHead = 0;
            int ringTail = 0;
            int ringCountFrames = 0;

            byte[] scratchBuffer = Array.Empty<byte>();
            byte[] outChunk = Array.Empty<byte>();
            uint maxBufferedFrames = sampleRate / 18; // ~55ms render buffer cap to minimize jitter
            int silentIdleTicks = 0;

            while (!token.IsCancellationRequested)
            {
                if (captureClient.GetNextPacketSize(out uint packetFrames) != 0)
                {
                    break;
                }

                if (packetFrames == 0)
                {
                    silentIdleTicks++;
                    if (silentIdleTicks > 15 && ringCountFrames > 0)
                    {
                        // Flush stale delayed audio after ~100ms of complete silence
                        ringHead = 0;
                        ringTail = 0;
                        ringCountFrames = 0;
                    }
                    Thread.Sleep(silentIdleTicks > 15 ? 12 : 4);
                    continue;
                }

                silentIdleTicks = 0;

                while (packetFrames > 0 && !token.IsCancellationRequested)
                {
                    int hrGet = captureClient.GetBuffer(out IntPtr capData, out uint framesRead, out uint flags, out _, out _);
                    if (hrGet != 0)
                    {
                        break;
                    }

                    int readFrames = (int)framesRead;
                    int byteCount = readFrames * blockAlign;
                    if (scratchBuffer.Length < byteCount)
                    {
                        scratchBuffer = new byte[byteCount];
                    }

                    bool isSilent = (flags & AudClntBufferFlagsSilent) != 0 || capData == IntPtr.Zero;
                    if (isSilent)
                    {
                        Array.Clear(scratchBuffer, 0, byteCount);
                    }
                    else
                    {
                        Marshal.Copy(capData, scratchBuffer, 0, byteCount);
                    }

                    captureClient.ReleaseBuffer(framesRead);

                    // Push incoming frames into delay ring
                    if (readFrames >= maxDelayFrames)
                    {
                        ringHead = 0;
                        ringTail = 0;
                        ringCountFrames = 0;
                    }
                    else
                    {
                        int overflow = (ringCountFrames + readFrames) - maxDelayFrames;
                        if (overflow > 0)
                        {
                            ringHead = (ringHead + overflow) % maxDelayFrames;
                            ringCountFrames -= overflow;
                        }

                        int firstFrames = Math.Min(readFrames, maxDelayFrames - ringTail);
                        int firstBytes = firstFrames * blockAlign;
                        Buffer.BlockCopy(scratchBuffer, 0, delayRing, ringTail * blockAlign, firstBytes);
                        int remFrames = readFrames - firstFrames;
                        if (remFrames > 0)
                        {
                            Buffer.BlockCopy(scratchBuffer, firstBytes, delayRing, 0, remFrames * blockAlign);
                        }
                        ringTail = (ringTail + readFrames) % maxDelayFrames;
                        ringCountFrames += readFrames;
                    }

                    int desiredDelayFrames = (int)((_syncDelayMs * (long)sampleRate) / 1000L);
                    desiredDelayFrames = Math.Clamp(desiredDelayFrames, 0, maxDelayFrames - readFrames - 1);

                    // If user lowered the delay slider, trim excess older frames smoothly
                    int maxAllowedFrames = desiredDelayFrames + readFrames * 2;
                    if (ringCountFrames > maxAllowedFrames && maxAllowedFrames >= 0)
                    {
                        int drop = ringCountFrames - (desiredDelayFrames + readFrames);
                        if (drop > 0 && drop <= ringCountFrames)
                        {
                            ringHead = (ringHead + drop) % maxDelayFrames;
                            ringCountFrames -= drop;
                        }
                    }

                    // Emit once ring has accumulated at least desiredDelayFrames
                    if (ringCountFrames > desiredDelayFrames)
                    {
                        int framesToEmit = Math.Min(readFrames, ringCountFrames - desiredDelayFrames);
                        int emitBytes = framesToEmit * blockAlign;
                        if (outChunk.Length < emitBytes)
                        {
                            outChunk = new byte[emitBytes];
                        }

                        int firstOutFrames = Math.Min(framesToEmit, maxDelayFrames - ringHead);
                        int firstOutBytes = firstOutFrames * blockAlign;
                        Buffer.BlockCopy(delayRing, ringHead * blockAlign, outChunk, 0, firstOutBytes);
                        int remOutFrames = framesToEmit - firstOutFrames;
                        if (remOutFrames > 0)
                        {
                            Buffer.BlockCopy(delayRing, 0, outChunk, firstOutBytes, remOutFrames * blockAlign);
                        }
                        ringHead = (ringHead + framesToEmit) % maxDelayFrames;
                        ringCountFrames -= framesToEmit;

                        uint emitU = (uint)framesToEmit;
                        foreach (var target in renderTargets)
                        {
                            if (target.Client.GetCurrentPadding(out uint padding) != 0)
                            {
                                continue;
                            }

                            uint available = target.BufferFrames > padding ? target.BufferFrames - padding : 0;
                            if (padding > maxBufferedFrames || available < emitU)
                            {
                                continue;
                            }

                            if (target.Render.GetBuffer(emitU, out IntPtr renPtr) == 0 && renPtr != IntPtr.Zero)
                            {
                                Marshal.Copy(outChunk, 0, renPtr, emitBytes);
                                target.Render.ReleaseBuffer(emitU, 0);
                            }
                        }
                    }

                    if (captureClient.GetNextPacketSize(out packetFrames) != 0)
                    {
                        break;
                    }
                }
            }
        }
        catch
        {
            // Exit mirror worker gracefully on device removal or shutdown
        }
        finally
        {
            try { captureAudioClient?.Stop(); } catch { }
            foreach (var target in renderTargets)
            {
                try { target.Client.Stop(); } catch { }
                Marshal.ReleaseComObject(target.Render);
                Marshal.ReleaseComObject(target.Client);
                Marshal.ReleaseComObject(target.Device);
            }

            if (mixFormatPtr != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(mixFormatPtr);
            }
            if (captureClient != null) Marshal.ReleaseComObject(captureClient);
            if (captureAudioClient != null) Marshal.ReleaseComObject(captureAudioClient);
            if (primaryDev != null) Marshal.ReleaseComObject(primaryDev);
            if (enumerator != null) Marshal.ReleaseComObject(enumerator);
        }
    }

    public void Dispose()
    {
        StopMirroring();
    }
}
