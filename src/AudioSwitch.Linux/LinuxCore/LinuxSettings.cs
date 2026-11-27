using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AudioSwitch.Linux.LinuxCore;

internal sealed class LinuxSettings
{
    private static readonly byte[] AssociatedData =
        Encoding.UTF8.GetBytes("AudioSwitch.Linux::Settings::v1");

    public static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AudioSwitch");

    private static string EncryptedPath => Path.Combine(ConfigDir, "settings.dat");
    private static string KeyPath => Path.Combine(ConfigDir, ".key");

    public bool ShowMediaPlayer { get; set; } = true;
    public bool ShowBatteryPage { get; set; } = true;
    public bool MuteInactiveDevices { get; set; } = true;
    public bool HideMusicToTray { get; set; } = true;
    public bool UseYouTubeMusicDefault { get; set; } = false;
    public int SyncDelayMs { get; set; } = 170;

    public static LinuxSettings Load()
    {
        try
        {
            if (!File.Exists(EncryptedPath) || !File.Exists(KeyPath))
            {
                return new LinuxSettings();
            }

            byte[] key = Convert.FromBase64String(File.ReadAllText(KeyPath, Encoding.UTF8).Trim());
            byte[] blob = File.ReadAllBytes(EncryptedPath);
            byte[]? plain = DecryptWithAad(key, blob, AssociatedData);
            return plain != null
                ? JsonSerializer.Deserialize<LinuxSettings>(plain) ?? new LinuxSettings()
                : new LinuxSettings();
        }
        catch
        {
            // Tampered or corrupt encrypted payload safely falls back to defaults
            return new LinuxSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            byte[] plain = JsonSerializer.SerializeToUtf8Bytes(this);
            byte[] output = EncryptWithAad(GetOrCreateKey(), plain, AssociatedData);
            File.WriteAllBytes(EncryptedPath, output);
            RestrictOwnerOnly(EncryptedPath, readOnly: false);
        }
        catch { }
    }

    public static byte[] EncryptWithAad(byte[] key, byte[] plain, byte[] aad)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] tag = new byte[16];
        byte[] cipher = new byte[plain.Length];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, cipher, tag, aad);

        byte[] output = new byte[12 + 16 + cipher.Length];
        nonce.CopyTo(output, 0);
        tag.CopyTo(output, 12);
        cipher.CopyTo(output, 28);
        return output;
    }

    public static byte[]? DecryptWithAad(byte[] key, byte[] blob, byte[] aad)
    {
        if (key.Length != 32 || blob.Length < 28) return null;

        ReadOnlySpan<byte> nonce = blob.AsSpan(0, 12);
        ReadOnlySpan<byte> tag = blob.AsSpan(12, 16);
        ReadOnlySpan<byte> cipher = blob.AsSpan(28);
        byte[] plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain, aad);
        return plain;
    }

    public static byte[] GetOrCreateKey()
    {
        if (File.Exists(KeyPath))
        {
            try
            {
                byte[] existing = Convert.FromBase64String(File.ReadAllText(KeyPath, Encoding.UTF8).Trim());
                if (existing.Length == 32) return existing;
            }
            catch { }
        }

        byte[] key = RandomNumberGenerator.GetBytes(32);
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(KeyPath, Convert.ToBase64String(key), Encoding.UTF8);
        RestrictOwnerOnly(KeyPath, readOnly: false);
        return key;
    }

    public static void RestrictOwnerOnly(string filePath, bool readOnly)
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                File.SetUnixFileMode(
                    filePath,
                    readOnly
                        ? UnixFileMode.UserRead
                        : UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch { }
        }
    }
}
