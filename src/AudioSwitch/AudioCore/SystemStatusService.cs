using System.Runtime.InteropServices;
using System.Text;
using Forms = System.Windows.Forms;

namespace AudioSwitch.AudioCore;

internal sealed record BluetoothBatteryInfo(string DeviceName, int Percent);

internal sealed record SystemTelemetrySnapshot(
    int LaptopBatteryPercent,
    bool HasLaptopBattery,
    bool IsPluggedIn,
    int RamUsagePercent,
    int SystemCpuPercent,
    IReadOnlyList<BluetoothBatteryInfo> BluetoothBatteries);

/// <summary>
/// Reads laptop power status, RAM/CPU utilization, and Bluetooth headset battery percentages
/// via standard Windows APIs (only queried while expanded; zero idle polling).
/// </summary>
internal sealed class SystemStatusService
{
    private const uint DigcfPresent = 0x00000002;
    private static readonly Guid GuidDevClassSystem = new("4D36E97D-E325-11CE-BFC1-08002BE10318");
    private static readonly Guid GuidDevClassBluetooth = new("E0CBF06C-CD8B-4647-BB8A-263B43F0F974");

    private static readonly DevPropKey DevPkeyFriendlyName =
        new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    private static readonly DevPropKey DevPkeyDeviceDesc =
        new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 2);

    private static readonly DevPropKey DevPkeyBluetoothBattery =
        new(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);

    // DEVPKEY_Device_IsConnected (DEVPROP_TYPE_BOOLEAN = 17)
    private static readonly DevPropKey DevPkeyIsConnected =
        new(new Guid("83DA6326-97A6-4088-9453-A1923F573B29"), 15);

    private ulong _prevIdle;
    private ulong _prevKernel;
    private ulong _prevUser;
    private bool _hasPrevCpu;

    public SystemTelemetrySnapshot CaptureSnapshot(bool includeBluetoothBattery = true)
    {
        var power = Forms.SystemInformation.PowerStatus;
        bool hasBattery = (power.BatteryChargeStatus & Forms.BatteryChargeStatus.NoSystemBattery) == 0
                          && power.BatteryLifePercent <= 1.0f
                          && power.BatteryLifePercent >= 0f;
        int batteryPct = hasBattery ? (int)Math.Round(power.BatteryLifePercent * 100f) : 0;
        bool pluggedIn = power.PowerLineStatus == Forms.PowerLineStatus.Online;

        int ramPct = GetMemoryLoadPercent();
        int cpuPct = SampleSystemCpuPercent();

        IReadOnlyList<BluetoothBatteryInfo> btBatteries = includeBluetoothBattery
            ? ReadBluetoothBatteries()
            : Array.Empty<BluetoothBatteryInfo>();

        return new SystemTelemetrySnapshot(
            batteryPct,
            hasBattery,
            pluggedIn,
            ramPct,
            cpuPct,
            btBatteries);
    }

    public static IReadOnlyList<BluetoothBatteryInfo> ReadBluetoothBatteries()
    {
        var results = new List<BluetoothBatteryInfo>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ScanClassForBattery(GuidDevClassSystem, results, seenNames);
        ScanClassForBattery(GuidDevClassBluetooth, results, seenNames);

        return results;
    }

    private static void ScanClassForBattery(
        Guid classGuid,
        List<BluetoothBatteryInfo> results,
        HashSet<string> seenNames)
    {
        IntPtr devInfo = SetupDiGetClassDevsW(ref classGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent);
        if (devInfo == IntPtr.Zero || devInfo == new IntPtr(-1))
        {
            return;
        }

        try
        {
            var devData = new SpDevInfoData { cbSize = (uint)Marshal.SizeOf<SpDevInfoData>() };
            var batteryKey = DevPkeyBluetoothBattery;
            var connectedKey = DevPkeyIsConnected;
            var nameKey = DevPkeyFriendlyName;
            var descKey = DevPkeyDeviceDesc;
            byte[] buf = new byte[512];

            for (uint index = 0; SetupDiEnumDeviceInfo(devInfo, index, ref devData); index++)
            {
                if (!IsDeviceConnected(devInfo, ref devData, ref connectedKey, buf))
                {
                    continue;
                }

                if (!SetupDiGetDevicePropertyW(
                        devInfo,
                        ref devData,
                        ref batteryKey,
                        out uint propType,
                        buf,
                        (uint)buf.Length,
                        out uint reqSize,
                        0))
                {
                    continue;
                }

                // DEVPROP_TYPE_BYTE = 3, DEVPROP_TYPE_UINT32 = 7
                int pct = -1;
                if (reqSize >= 1 && (propType == 3 || propType == 7 || propType == 6))
                {
                    pct = buf[0];
                }
                if (pct < 0 || pct > 100) continue;

                string? rawName = ReadStringProperty(devInfo, ref devData, ref nameKey, buf)
                                  ?? ReadStringProperty(devInfo, ref devData, ref descKey, buf);
                if (string.IsNullOrWhiteSpace(rawName)) continue;

                string cleanName = rawName
                    .Replace(" Hands-Free AG", "", StringComparison.OrdinalIgnoreCase)
                    .Replace(" Hands-Free", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();

                if (seenNames.Add(cleanName))
                {
                    results.Add(new BluetoothBatteryInfo(cleanName, pct));
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(devInfo);
        }
    }

    private static bool IsDeviceConnected(
        IntPtr devInfo,
        ref SpDevInfoData devData,
        ref DevPropKey connectedKey,
        byte[] buf)
    {
        if (!SetupDiGetDevicePropertyW(
                devInfo,
                ref devData,
                ref connectedKey,
                out uint propType,
                buf,
                (uint)buf.Length,
                out uint reqSize,
                0))
        {
            return false;
        }

        // DEVPROP_TYPE_BOOLEAN = 0x11 (17)
        return reqSize >= 1 && (propType == 17 || propType == 3 || propType == 7) && buf[0] != 0;
    }

    private static string? ReadStringProperty(
        IntPtr devInfo,
        ref SpDevInfoData devData,
        ref DevPropKey key,
        byte[] buf)
    {
        if (!SetupDiGetDevicePropertyW(devInfo, ref devData, ref key, out _, buf, (uint)buf.Length, out uint reqSize, 0))
        {
            return null;
        }
        if (reqSize < 2) return null;
        return Encoding.Unicode.GetString(buf, 0, (int)Math.Min(reqSize, (uint)buf.Length)).TrimEnd('\0');
    }

    private static int GetMemoryLoadPercent()
    {
        var mem = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref mem) ? (int)mem.dwMemoryLoad : 0;
    }

    private int SampleSystemCpuPercent()
    {
        if (!GetSystemTimes(out FileTime idleFt, out FileTime kernelFt, out FileTime userFt))
        {
            return 0;
        }

        ulong idle = idleFt.ToUInt64();
        ulong kernel = kernelFt.ToUInt64();
        ulong user = userFt.ToUInt64();

        if (!_hasPrevCpu)
        {
            _prevIdle = idle;
            _prevKernel = kernel;
            _prevUser = user;
            _hasPrevCpu = true;
            return 0;
        }

        ulong idleDiff = idle - _prevIdle;
        ulong totalDiff = (kernel - _prevKernel) + (user - _prevUser);

        _prevIdle = idle;
        _prevKernel = kernel;
        _prevUser = user;

        if (totalDiff == 0 || idleDiff > totalDiff) return 0;
        return (int)Math.Clamp(Math.Round((totalDiff - idleDiff) * 100.0 / totalDiff), 0, 100);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropKey
    {
        public Guid fmtid;
        public uint pid;

        public DevPropKey(Guid formatId, uint propertyId)
        {
            fmtid = formatId;
            pid = propertyId;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevInfoData
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public ulong ToUInt64() => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SpDevInfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDevicePropertyW(
        IntPtr deviceInfoSet,
        ref SpDevInfoData deviceInfoData,
        ref DevPropKey propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime lpIdleTime, out FileTime lpKernelTime, out FileTime lpUserTime);
}
