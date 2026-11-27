using System.Text.Json;

namespace AudioSwitch.Linux.LinuxCore;

internal enum LinuxAudioCategory
{
    AuxWired,
    Bluetooth,
    BuiltInSpeakers,
    BuiltInMic,
    Other
}

internal sealed record LinuxAudioEndpoint(
    string Name,
    string FriendlyName,
    LinuxAudioCategory Category,
    bool IsDefault,
    bool IsMuted,
    int VolumePercent,
    bool IsAvailable);

internal sealed record LinuxAudioStreamApp(
    uint StreamIndex,
    string AppName,
    string BinaryName,
    string CurrentSinkOrSourceName,
    bool IsCapture);

internal sealed class PipeWireAudioService
{
    private readonly List<int> _mirrorModuleIds = new();
    private readonly HashSet<string> _pinnedSinks = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pinnedSources = new(StringComparer.OrdinalIgnoreCase);

    public bool IsMirroring => _mirrorModuleIds.Count > 0;

    public IReadOnlyList<LinuxAudioEndpoint> GetSinks()
    {
        string defaultSink = GetDefaultName("get-default-sink");
        var (code, json, _) = LinuxCommandRunner.Run("pactl", ["-f", "json", "list", "sinks"]);
        if (code == 0 && !string.IsNullOrWhiteSpace(json))
        {
            return ParseEndpointsJson(json, defaultSink, isSource: false);
        }

        return ParseEndpointsShort("sinks", defaultSink, isSource: false);
    }

    public IReadOnlyList<LinuxAudioEndpoint> GetSources()
    {
        string defaultSource = GetDefaultName("get-default-source");
        var (code, json, _) = LinuxCommandRunner.Run("pactl", ["-f", "json", "list", "sources"]);
        if (code == 0 && !string.IsNullOrWhiteSpace(json))
        {
            return ParseEndpointsJson(json, defaultSource, isSource: true);
        }

        return ParseEndpointsShort("sources", defaultSource, isSource: true);
    }

    public bool SwitchDefaultSink(string targetSinkName, bool autoMuteOthers)
    {
        var (code, _, _) = LinuxCommandRunner.Run("pactl", ["set-default-sink", targetSinkName]);
        if (code != 0) return false;

        LinuxCommandRunner.Run("pactl", ["set-sink-mute", targetSinkName, "0"]);

        // Move active unpinned streams immediately
        foreach (var stream in GetSinkInputs())
        {
            if (_pinnedSinks.Contains(stream.CurrentSinkOrSourceName)) continue;
            LinuxCommandRunner.Run("pactl", ["move-sink-input", stream.StreamIndex.ToString(), targetSinkName]);
        }

        if (autoMuteOthers)
        {
            foreach (var sink in GetSinks())
            {
                if (!sink.Name.Equals(targetSinkName, StringComparison.OrdinalIgnoreCase) &&
                    !_pinnedSinks.Contains(sink.Name))
                {
                    LinuxCommandRunner.Run("pactl", ["set-sink-mute", sink.Name, "1"]);
                }
            }
        }

        return true;
    }

    public bool SwitchDefaultSource(string targetSourceName, bool autoMuteOthers)
    {
        var (code, _, _) = LinuxCommandRunner.Run("pactl", ["set-default-source", targetSourceName]);
        if (code != 0) return false;

        LinuxCommandRunner.Run("pactl", ["set-source-mute", targetSourceName, "0"]);

        foreach (var stream in GetSourceOutputs())
        {
            if (_pinnedSources.Contains(stream.CurrentSinkOrSourceName)) continue;
            LinuxCommandRunner.Run("pactl", ["move-source-output", stream.StreamIndex.ToString(), targetSourceName]);
        }

        if (autoMuteOthers)
        {
            foreach (var src in GetSources())
            {
                if (!src.Name.Equals(targetSourceName, StringComparison.OrdinalIgnoreCase) &&
                    !_pinnedSources.Contains(src.Name))
                {
                    LinuxCommandRunner.Run("pactl", ["set-source-mute", src.Name, "1"]);
                }
            }
        }

        return true;
    }

    public void SetVolume(string name, int percent, bool isSource = false)
    {
        int clamped = Math.Clamp(percent, 0, 150);
        string cmd = isSource ? "set-source-volume" : "set-sink-volume";
        LinuxCommandRunner.Run("pactl", [cmd, name, $"{clamped}%"]);
    }

    public void SetMute(string name, bool mute, bool isSource = false)
    {
        string cmd = isSource ? "set-source-mute" : "set-sink-mute";
        LinuxCommandRunner.Run("pactl", [cmd, name, mute ? "1" : "0"]);
    }

    public bool StartMirror(string primarySink, IEnumerable<string> secondarySinks, int delayMs)
    {
        StopMirror();
        int clampedDelay = Math.Clamp(delayMs, 0, 350);
        string monitorSource = $"{primarySink}.monitor";

        LinuxCommandRunner.Run("pactl", ["set-default-sink", primarySink]);
        LinuxCommandRunner.Run("pactl", ["set-sink-mute", primarySink, "0"]);

        foreach (string sec in secondarySinks)
        {
            if (string.IsNullOrWhiteSpace(sec) || sec.Equals(primarySink, StringComparison.OrdinalIgnoreCase)) continue;
            LinuxCommandRunner.Run("pactl", ["set-sink-mute", sec, "0"]);

            var (code, outStr, _) = LinuxCommandRunner.Run("pactl",
            [
                "load-module",
                "module-loopback",
                $"source={monitorSource}",
                $"sink={sec}",
                $"latency_msec={clampedDelay}"
            ]);

            if (code == 0 && int.TryParse(outStr.Trim(), out int moduleId))
            {
                _mirrorModuleIds.Add(moduleId);
            }
        }

        return _mirrorModuleIds.Count > 0;
    }

    public void StopMirror()
    {
        foreach (int id in _mirrorModuleIds)
        {
            LinuxCommandRunner.Run("pactl", ["unload-module", id.ToString()]);
        }
        _mirrorModuleIds.Clear();
    }

    public IReadOnlyList<LinuxAudioStreamApp> GetSinkInputs()
    {
        var list = new List<LinuxAudioStreamApp>();
        var sinksByIndex = GetSinkIndexToNameMap(isSource: false);

        var (code, json, _) = LinuxCommandRunner.Run("pactl", ["-f", "json", "list", "sink-inputs"]);
        if (code != 0 || string.IsNullOrWhiteSpace(json)) return list;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                uint idx = el.TryGetProperty("index", out var iEl) && iEl.TryGetUInt32(out uint u) ? u : 0;
                if (idx == 0) continue;

                string sinkIdx = el.TryGetProperty("sink", out var sEl) ? sEl.ToString() : string.Empty;
                string sinkName = sinksByIndex.TryGetValue(sinkIdx, out string? n) ? n : sinkIdx;

                string appName = "App";
                string binName = string.Empty;
                if (el.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
                {
                    if (props.TryGetProperty("application.name", out var an)) appName = an.GetString() ?? appName;
                    if (props.TryGetProperty("application.process.binary", out var bn)) binName = bn.GetString() ?? string.Empty;
                }

                list.Add(new LinuxAudioStreamApp(idx, appName, binName, sinkName, IsCapture: false));
            }
        }
        catch { }

        return list;
    }

    public IReadOnlyList<LinuxAudioStreamApp> GetSourceOutputs()
    {
        var list = new List<LinuxAudioStreamApp>();
        var sourcesByIndex = GetSinkIndexToNameMap(isSource: true);

        var (code, json, _) = LinuxCommandRunner.Run("pactl", ["-f", "json", "list", "source-outputs"]);
        if (code != 0 || string.IsNullOrWhiteSpace(json)) return list;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                uint idx = el.TryGetProperty("index", out var iEl) && iEl.TryGetUInt32(out uint u) ? u : 0;
                if (idx == 0) continue;

                string srcIdx = el.TryGetProperty("source", out var sEl) ? sEl.ToString() : string.Empty;
                string srcName = sourcesByIndex.TryGetValue(srcIdx, out string? n) ? n : srcIdx;

                string appName = "Mic App";
                string binName = string.Empty;
                if (el.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
                {
                    if (props.TryGetProperty("application.name", out var an)) appName = an.GetString() ?? appName;
                    if (props.TryGetProperty("application.process.binary", out var bn)) binName = bn.GetString() ?? string.Empty;
                }

                if (appName.Contains("Peak detect", StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(new LinuxAudioStreamApp(idx, appName, binName, srcName, IsCapture: true));
            }
        }
        catch { }

        return list;
    }

    public bool MoveAppOutput(uint streamIndex, string targetSinkName)
    {
        LinuxCommandRunner.Run("pactl", ["set-sink-mute", targetSinkName, "0"]);
        _pinnedSinks.Add(targetSinkName);
        return LinuxCommandRunner.Run("pactl", ["move-sink-input", streamIndex.ToString(), targetSinkName]).ExitCode == 0;
    }

    public bool MoveAppInput(uint streamIndex, string targetSourceName)
    {
        LinuxCommandRunner.Run("pactl", ["set-source-mute", targetSourceName, "0"]);
        _pinnedSources.Add(targetSourceName);
        return LinuxCommandRunner.Run("pactl", ["move-source-output", streamIndex.ToString(), targetSourceName]).ExitCode == 0;
    }

    public (int MediaMoved, int CallsMoved) AutoDualSplit(string auxSink, string btSink, string? btMicSource)
    {
        LinuxCommandRunner.Run("pactl", ["set-sink-mute", auxSink, "0"]);
        LinuxCommandRunner.Run("pactl", ["set-sink-mute", btSink, "0"]);
        if (!string.IsNullOrEmpty(btMicSource)) LinuxCommandRunner.Run("pactl", ["set-source-mute", btMicSource, "0"]);

        int mediaMoved = 0;
        int callsMoved = 0;

        foreach (var s in GetSinkInputs())
        {
            if (IsCallApp(s.AppName, s.BinaryName))
            {
                if (MoveAppOutput(s.StreamIndex, btSink)) callsMoved++;
            }
            else
            {
                if (MoveAppOutput(s.StreamIndex, auxSink)) mediaMoved++;
            }
        }

        if (!string.IsNullOrEmpty(btMicSource))
        {
            foreach (var micStream in GetSourceOutputs())
            {
                if (IsCallApp(micStream.AppName, micStream.BinaryName))
                {
                    MoveAppInput(micStream.StreamIndex, btMicSource);
                }
            }
        }

        return (mediaMoved, callsMoved);
    }

    public static bool IsCallApp(string appName, string binaryName)
    {
        string combo = $"{appName} {binaryName}".ToLowerInvariant();
        return combo.Contains("discord") || combo.Contains("webrtc") || combo.Contains("zoom") || combo.Contains("teams");
    }

    private static string GetDefaultName(string subCmd)
    {
        var (code, outStr, _) = LinuxCommandRunner.Run("pactl", [subCmd]);
        return code == 0 ? outStr.Trim() : string.Empty;
    }

    private static Dictionary<string, string> GetSinkIndexToNameMap(bool isSource)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var (code, shortOut, _) = LinuxCommandRunner.Run("pactl", ["list", "short", isSource ? "sources" : "sinks"]);
        if (code != 0) return map;

        foreach (string line in shortOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                map[parts[0].Trim()] = parts[1].Trim();
            }
        }
        return map;
    }

    private static IReadOnlyList<LinuxAudioEndpoint> ParseEndpointsJson(string json, string defaultName, bool isSource)
    {
        var list = new List<LinuxAudioEndpoint>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                string name = el.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (isSource && name.EndsWith(".monitor", StringComparison.OrdinalIgnoreCase)) continue;

                string desc = el.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? name : name;
                string activePort = el.TryGetProperty("active_port", out var apEl) ? apEl.GetString() ?? string.Empty : string.Empty;
                bool mute = el.TryGetProperty("mute", out var mEl) && mEl.ValueKind == JsonValueKind.True;

                int volPct = 100;
                if (el.TryGetProperty("volume", out var volObj) && volObj.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in volObj.EnumerateObject())
                    {
                        if (prop.Value.TryGetProperty("value_percent", out var vpEl))
                        {
                            string vp = (vpEl.GetString() ?? "100%").Replace("%", "").Trim();
                            if (int.TryParse(vp, out int parsed)) volPct = parsed;
                            break;
                        }
                    }
                }

                bool available = true;
                if (el.TryGetProperty("ports", out var portsEl) && portsEl.ValueKind == JsonValueKind.Array && !string.IsNullOrEmpty(activePort))
                {
                    foreach (var p in portsEl.EnumerateArray())
                    {
                        if (p.TryGetProperty("name", out var pn) && pn.GetString() == activePort &&
                            p.TryGetProperty("availability", out var avail))
                        {
                            available = !string.Equals(avail.GetString(), "not available", StringComparison.OrdinalIgnoreCase);
                            break;
                        }
                    }
                }

                var cat = ClassifyEndpoint(name, desc, activePort, isSource);
                list.Add(new LinuxAudioEndpoint(
                    name,
                    desc,
                    cat,
                    string.Equals(name, defaultName, StringComparison.OrdinalIgnoreCase),
                    mute,
                    volPct,
                    available));
            }
        }
        catch { }

        return list;
    }

    private static IReadOnlyList<LinuxAudioEndpoint> ParseEndpointsShort(string target, string defaultName, bool isSource)
    {
        var list = new List<LinuxAudioEndpoint>();
        var (code, outStr, _) = LinuxCommandRunner.Run("pactl", ["list", "short", target]);
        if (code != 0) return list;

        foreach (string line in outStr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            string name = parts[1].Trim();
            if (isSource && name.EndsWith(".monitor", StringComparison.OrdinalIgnoreCase)) continue;
            var cat = ClassifyEndpoint(name, name, string.Empty, isSource);
            list.Add(new LinuxAudioEndpoint(
                name,
                name,
                cat,
                string.Equals(name, defaultName, StringComparison.OrdinalIgnoreCase),
                false,
                80,
                true));
        }

        return list;
    }

    private static LinuxAudioCategory ClassifyEndpoint(string name, string desc, string activePort, bool isSource)
    {
        string all = $"{name} {desc} {activePort}".ToLowerInvariant();
        if (all.Contains("bluez") || all.Contains("bluetooth")) return LinuxAudioCategory.Bluetooth;

        if (!isSource)
        {
            if (activePort.Contains("headphone", StringComparison.OrdinalIgnoreCase) ||
                activePort.Contains("lineout", StringComparison.OrdinalIgnoreCase) ||
                all.Contains("headphone"))
            {
                return LinuxAudioCategory.AuxWired;
            }
            if (activePort.Contains("speaker", StringComparison.OrdinalIgnoreCase) || all.Contains("speaker"))
            {
                return LinuxAudioCategory.BuiltInSpeakers;
            }
            if (all.Contains("analog")) return LinuxAudioCategory.AuxWired;
        }
        else
        {
            if (activePort.Contains("headset-mic", StringComparison.OrdinalIgnoreCase) ||
                activePort.Contains("mic", StringComparison.OrdinalIgnoreCase) && all.Contains("jack"))
            {
                return LinuxAudioCategory.AuxWired;
            }
            if (all.Contains("analog") || all.Contains("internal")) return LinuxAudioCategory.BuiltInMic;
        }

        return LinuxAudioCategory.Other;
    }
}
