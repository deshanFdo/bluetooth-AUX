using System.Globalization;
using System.Text.RegularExpressions;

namespace AudioSwitch.Linux.LinuxCore;

internal sealed record LinuxBluetoothBattery(string DeviceName, int Percent);

internal sealed record LinuxTelemetrySnapshot(
    int LaptopBatteryPercent,
    bool HasLaptopBattery,
    bool IsPluggedIn,
    int RamUsagePercent,
    int CpuUsagePercent,
    IReadOnlyList<LinuxBluetoothBattery> BluetoothBatteries);

internal sealed class LinuxTelemetryService
{
    private ulong _prevTotal;
    private ulong _prevIdle;
    private bool _hasPrevCpu;

    public LinuxTelemetrySnapshot CaptureSnapshot()
    {
        var (hasBat, batPct, plugged) = ReadLaptopBattery();
        int ramPct = ReadMemoryUsagePercent();
        int cpuPct = SampleCpuPercent();
        var btBats = ReadConnectedBluetoothBatteries();
        return new LinuxTelemetrySnapshot(batPct, hasBat, plugged, ramPct, cpuPct, btBats);
    }

    private static (bool HasBattery, int Percent, bool PluggedIn) ReadLaptopBattery()
    {
        try
        {
            const string powerDir = "/sys/class/power_supply";
            if (!Directory.Exists(powerDir)) return (false, 0, true);

            bool plugged = false;
            foreach (string ac in Directory.EnumerateDirectories(powerDir, "AC*").Concat(Directory.EnumerateDirectories(powerDir, "ADP*")))
            {
                string onlinePath = Path.Combine(ac, "online");
                if (File.Exists(onlinePath) && File.ReadAllText(onlinePath).Trim() == "1")
                {
                    plugged = true;
                    break;
                }
            }

            foreach (string bat in Directory.EnumerateDirectories(powerDir, "BAT*"))
            {
                string capPath = Path.Combine(bat, "capacity");
                if (!File.Exists(capPath)) continue;
                if (int.TryParse(File.ReadAllText(capPath).Trim(), out int cap))
                {
                    string statusPath = Path.Combine(bat, "status");
                    string status = File.Exists(statusPath) ? File.ReadAllText(statusPath).Trim() : string.Empty;
                    if (status.Equals("Charging", StringComparison.OrdinalIgnoreCase) ||
                        status.Equals("Full", StringComparison.OrdinalIgnoreCase))
                    {
                        plugged = true;
                    }
                    return (true, Math.Clamp(cap, 0, 100), plugged);
                }
            }
            return (false, 0, plugged);
        }
        catch
        {
            return (false, 0, false);
        }
    }

    private static int ReadMemoryUsagePercent()
    {
        try
        {
            if (!File.Exists("/proc/meminfo")) return 0;
            ulong total = 0;
            ulong avail = 0;

            foreach (string line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                {
                    total = ParseMemInfoKb(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                {
                    avail = ParseMemInfoKb(line);
                }
                if (total > 0 && avail > 0) break;
            }

            if (total == 0) return 0;
            return (int)Math.Clamp(Math.Round((total - avail) * 100.0 / total), 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private static ulong ParseMemInfoKb(string line)
    {
        var match = Regex.Match(line, @"\d+");
        return match.Success && ulong.TryParse(match.Value, out ulong val) ? val : 0;
    }

    private int SampleCpuPercent()
    {
        try
        {
            if (!File.Exists("/proc/stat")) return 0;
            string? first = File.ReadLines("/proc/stat").FirstOrDefault();
            if (string.IsNullOrWhiteSpace(first)) return 0;

            string[] parts = first.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) return 0;

            ulong total = 0;
            for (int i = 1; i < parts.Length; i++)
            {
                if (ulong.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong v))
                {
                    total += v;
                }
            }

            ulong idle = ulong.Parse(parts[4], CultureInfo.InvariantCulture);
            if (parts.Length > 5 && ulong.TryParse(parts[5], out ulong iowait)) idle += iowait;

            if (!_hasPrevCpu)
            {
                _prevTotal = total;
                _prevIdle = idle;
                _hasPrevCpu = true;
                return 0;
            }

            ulong dTotal = total - _prevTotal;
            ulong dIdle = idle - _prevIdle;
            _prevTotal = total;
            _prevIdle = idle;

            if (dTotal == 0 || dIdle > dTotal) return 0;
            return (int)Math.Clamp(Math.Round((dTotal - dIdle) * 100.0 / dTotal), 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Reads connected-only Bluetooth headset batteries via BlueZ (org.bluez.Battery1) and UPower fallback.
    /// </summary>
    public static IReadOnlyList<LinuxBluetoothBattery> ReadConnectedBluetoothBatteries()
    {
        var list = new List<LinuxBluetoothBattery>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Primary on Ubuntu/BlueZ 5.64+: gdbus org.bluez objects gated by Connected = true
        var (code, outStr, _) = LinuxCommandRunner.Run("gdbus",
        [
            "call", "--system", "--dest", "org.bluez", "--object-path", "/",
            "--method", "org.freedesktop.DBus.ObjectManager.GetManagedObjects"
        ], 2500);

        if (code == 0 && !string.IsNullOrWhiteSpace(outStr))
        {
            foreach (Match devMatch in Regex.Matches(
                         outStr,
                         @"'/org/bluez/hci\d+/dev_[0-9A-F_]+':\s*\{(?<body>.*?)(?=(?:'/org/bluez/)|$)",
                         RegexOptions.Singleline))
            {
                string body = devMatch.Groups["body"].Value;
                if (!body.Contains("org.bluez.Battery1", StringComparison.Ordinal)) continue;
                if (!Regex.IsMatch(body, @"'Connected':\s*<true>")) continue;

                var pctMatch = Regex.Match(body, @"'org\.bluez\.Battery1'.*?'Percentage':\s*<byte\s+0x([0-9a-fA-F]+)|'Percentage':\s*<(?:byte\s+)?(\d+)>", RegexOptions.Singleline);
                int pct = -1;
                if (pctMatch.Groups[1].Success) pct = Convert.ToInt32(pctMatch.Groups[1].Value, 16);
                else if (pctMatch.Groups[2].Success) pct = int.Parse(pctMatch.Groups[2].Value);

                if (pct < 0 || pct > 100) continue;

                var aliasMatch = Regex.Match(body, @"'(?:Alias|Name)':\s*<'([^']+)'>");
                string name = aliasMatch.Success ? aliasMatch.Groups[1].Value.Trim() : "Bluetooth Headset";
                if (seen.Add(name))
                {
                    list.Add(new LinuxBluetoothBattery(name, pct));
                }
            }
        }

        // 2. Fallback via upower -i /org/freedesktop/UPower/devices/headset_*
        if (list.Count == 0 && LinuxCommandRunner.CommandExists("upower"))
        {
            var (uc, uOut, _) = LinuxCommandRunner.Run("upower", ["-e"], 2000);
            if (uc == 0)
            {
                foreach (string devPath in uOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!devPath.Contains("headset", StringComparison.OrdinalIgnoreCase) &&
                        !devPath.Contains("headphones", StringComparison.OrdinalIgnoreCase) &&
                        !devPath.Contains("battery_dev", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var (ic, info, _) = LinuxCommandRunner.Run("upower", ["-i", devPath.Trim()], 1500);
                    if (ic != 0) continue;

                    var modelM = Regex.Match(info, @"model:\s*(.+)");
                    var pctM = Regex.Match(info, @"percentage:\s*(\d+)%");
                    if (pctM.Success && int.TryParse(pctM.Groups[1].Value, out int pct))
                    {
                        string name = modelM.Success ? modelM.Groups[1].Value.Trim() : "Bluetooth Headset";
                        if (seen.Add(name))
                        {
                            list.Add(new LinuxBluetoothBattery(name, Math.Clamp(pct, 0, 100)));
                        }
                    }
                }
            }
        }

        return list;
    }
}
