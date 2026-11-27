using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace AudioSwitch.AudioCore;

internal sealed class IslandSettings
{
    private const uint CryptProtectUiForbidden = 0x01;

    private static readonly byte[] DpapiEntropy =
        Encoding.UTF8.GetBytes("AudioSwitch::ProtectedSettings::v1");

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AudioSwitch");

    private static readonly string EncryptedSettingsPath = Path.Combine(SettingsDir, "settings.dat");
    private static readonly string LegacyPlainPath = Path.Combine(SettingsDir, "settings.json");

    public bool ShowMediaPlayer { get; set; } = true;
    public bool ShowLaptopBattery { get; set; } = true;
    public bool ShowBluetoothBatteries { get; set; } = true;
    public bool ShowSystemStats { get; set; } = true;
    public bool ShowSpeakersCard { get; set; } = true;
    public bool ShowMultiOutput { get; set; } = true;
    public bool ShowPillEqualizer { get; set; } = true;
    public bool ShowPillMediaTitle { get; set; } = true;
    public bool ShowPillBattery { get; set; } = true;
    public bool MuteInactiveDevices { get; set; } = true;
    public bool HardDisableAux { get; set; } = false;
    public bool ShowAuxJackMic { get; set; } = false;
    public bool HideSpotifyToTray { get; set; } = true;
    public bool UseYouTubeMusicDefault { get; set; } = false;

    public static IslandSettings Load()
    {
        try
        {
            if (File.Exists(EncryptedSettingsPath))
            {
                byte[] protectedBytes = File.ReadAllBytes(EncryptedSettingsPath);
                byte[]? plain = UnprotectBytes(protectedBytes);
                if (plain != null)
                {
                    return JsonSerializer.Deserialize<IslandSettings>(plain) ?? new IslandSettings();
                }
            }
            else if (File.Exists(LegacyPlainPath))
            {
                string json = File.ReadAllText(LegacyPlainPath, Encoding.UTF8);
                var migrated = JsonSerializer.Deserialize<IslandSettings>(json) ?? new IslandSettings();
                migrated.Save();
                return migrated;
            }
        }
        catch
        {
            // Tampered or unreadable encrypted payload safely falls back to clean defaults
        }
        return new IslandSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            byte[] plain = JsonSerializer.SerializeToUtf8Bytes(this);
            byte[]? protectedBytes = ProtectBytes(plain);
            if (protectedBytes != null)
            {
                File.WriteAllBytes(EncryptedSettingsPath, protectedBytes);
                if (File.Exists(LegacyPlainPath))
                {
                    try { File.Delete(LegacyPlainPath); } catch { }
                }
            }
        }
        catch
        {
            // Ignore read-only disk errors
        }
    }

    private static byte[]? ProtectBytes(byte[] plain) => InvokeDpapi(plain, protect: true);

    private static byte[]? UnprotectBytes(byte[] cipher) => InvokeDpapi(cipher, protect: false);

    private static byte[]? InvokeDpapi(byte[] input, bool protect)
    {
        if (input.Length == 0) return null;

        var inHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
        var entHandle = GCHandle.Alloc(DpapiEntropy, GCHandleType.Pinned);
        var outBlob = default(DataBlob);

        try
        {
            var inBlob = new DataBlob { cbData = input.Length, pbData = inHandle.AddrOfPinnedObject() };
            var entBlob = new DataBlob { cbData = DpapiEntropy.Length, pbData = entHandle.AddrOfPinnedObject() };

            bool ok = protect
                ? CryptProtectData(ref inBlob, "AudioSwitchConfig", ref entBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outBlob)
                : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outBlob);

            if (!ok || outBlob.pbData == IntPtr.Zero || outBlob.cbData <= 0)
            {
                return null;
            }

            byte[] output = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, output, 0, outBlob.cbData);
            return output;
        }
        finally
        {
            if (outBlob.pbData != IntPtr.Zero)
            {
                LocalFree(outBlob.pbData);
            }
            if (entHandle.IsAllocated) entHandle.Free();
            if (inHandle.IsAllocated) inHandle.Free();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string? szDataDescr,
        ref DataBlob pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        out DataBlob pDataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        IntPtr ppszDataDescr,
        ref DataBlob pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        uint dwFlags,
        out DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
