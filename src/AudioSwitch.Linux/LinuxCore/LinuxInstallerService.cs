using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace AudioSwitch.Linux.LinuxCore;

internal static class LinuxInstallerService
{
    private static readonly byte[] InstallSealAad =
        Encoding.UTF8.GetBytes("AudioSwitch.Linux::InstallSeal::v1");

    public static string UserInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WaveDock");

    private static string LegacyUserInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AudioSwitch");

    private static string InstallSealPath => Path.Combine(LinuxSettings.ConfigDir, "install.sig");

    public static string DesktopEntryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "applications",
        "wavedock.desktop");

    public static string AutostartEntryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "autostart",
        "wavedock.desktop");

    private static string LegacyDesktopEntryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "applications",
        "audioswitch.desktop");

    private static string LegacyAutostartEntryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "autostart",
        "audioswitch.desktop");

    public static void InstallUserLocal(bool autostart = true)
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) return;

        Directory.CreateDirectory(UserInstallDir);
        string targetExe = Path.Combine(UserInstallDir, "WaveDock");
        if (!string.Equals(Path.GetFullPath(exe), Path.GetFullPath(targetExe), StringComparison.Ordinal))
        {
            if (OperatingSystem.IsLinux() && File.Exists(targetExe))
            {
                File.SetUnixFileMode(targetExe,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            File.Copy(exe, targetExe, overwrite: true);
        }

        SealInstalledBinary(targetExe);

        string desktopContent = $"""
            [Desktop Entry]
            Type=Application
            Name=WaveDock
            Comment=WaveDock Dynamic Island Audio Switcher, Split Audio & Spotify / YouTube Music Control
            Exec={targetExe}
            Terminal=false
            Categories=AudioVideo;Audio;Utility;
            StartupNotify=false
            """;

        Directory.CreateDirectory(Path.GetDirectoryName(DesktopEntryPath)!);
        File.WriteAllText(DesktopEntryPath, desktopContent);

        if (autostart)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AutostartEntryPath)!);
            File.WriteAllText(AutostartEntryPath, desktopContent);
        }
    }

    public static void UninstallAll()
    {
        try { if (File.Exists(DesktopEntryPath)) File.Delete(DesktopEntryPath); } catch { }
        try { if (File.Exists(AutostartEntryPath)) File.Delete(AutostartEntryPath); } catch { }
        try { if (File.Exists(LegacyDesktopEntryPath)) File.Delete(LegacyDesktopEntryPath); } catch { }
        try { if (File.Exists(LegacyAutostartEntryPath)) File.Delete(LegacyAutostartEntryPath); } catch { }

        if (OperatingSystem.IsLinux())
        {
            try
            {
                string targetExe = Path.Combine(UserInstallDir, "WaveDock");
                if (File.Exists(targetExe))
                {
                    File.SetUnixFileMode(targetExe,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                if (File.Exists(InstallSealPath))
                {
                    LinuxSettings.RestrictOwnerOnly(InstallSealPath, readOnly: false);
                }
            }
            catch { }
        }

        try { if (Directory.Exists(LinuxSettings.ConfigDir)) Directory.Delete(LinuxSettings.ConfigDir, recursive: true); } catch { }
        try { if (Directory.Exists(LinuxSettings.LegacyConfigDir)) Directory.Delete(LinuxSettings.LegacyConfigDir, recursive: true); } catch { }
        try { if (Directory.Exists(LegacyUserInstallDir)) Directory.Delete(LegacyUserInstallDir, recursive: true); } catch { }

        bool runningSystemDeb = File.Exists("/usr/lib/wavedock/WaveDock") || File.Exists("/usr/lib/audioswitch/AudioSwitch");
        string installDir = UserInstallDir;
        string? curExe = Environment.ProcessPath;

        if (Directory.Exists(installDir))
        {
            if (curExe != null && curExe.StartsWith(installDir, StringComparison.Ordinal))
            {
                // Spawn detached sh cleanup using positional args (zero shell string interpolation)
                var psi = new ProcessStartInfo("sh")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("sleep 1; chmod -R u+w \"$1\" \"$2\" 2>/dev/null || true; rm -rf \"$1\" \"$2\"");
                psi.ArgumentList.Add("wavedock-uninstall");
                psi.ArgumentList.Add(installDir);
                psi.ArgumentList.Add(LinuxSettings.ConfigDir);
                Process.Start(psi);
            }
            else
            {
                try { Directory.Delete(installDir, recursive: true); } catch { }
            }
        }

        if (runningSystemDeb && LinuxCommandRunner.CommandExists("pkexec"))
        {
            string pkg = File.Exists("/usr/lib/wavedock/WaveDock") ? "wavedock" : "audioswitch";
            LinuxCommandRunner.StartDetached("pkexec", ["apt-get", "purge", "-y", pkg]);
        }
    }

    private static void SealInstalledBinary(string targetExe)
    {
        try
        {
            if (File.Exists(targetExe))
            {
                Directory.CreateDirectory(LinuxSettings.ConfigDir);
                LinuxSettings.RestrictOwnerOnly(InstallSealPath, readOnly: false);

                string payload = ComputeBinaryDigestPayload(targetExe);
                byte[] encrypted = LinuxSettings.EncryptWithAad(
                    LinuxSettings.GetOrCreateKey(),
                    Encoding.UTF8.GetBytes(payload),
                    InstallSealAad);
                File.WriteAllBytes(InstallSealPath, encrypted);
                LinuxSettings.RestrictOwnerOnly(InstallSealPath, readOnly: true);

                if (OperatingSystem.IsLinux())
                {
                    File.SetUnixFileMode(targetExe, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                }
            }
        }
        catch { }
    }

    public static bool VerifyInstalledIntegrity()
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) return true;

        string userPrefix = Path.GetFullPath(UserInstallDir).TrimEnd('/') + "/";
        if (!Path.GetFullPath(exe).StartsWith(userPrefix, StringComparison.Ordinal)) return true;
        if (!File.Exists(InstallSealPath)) return true;

        try
        {
            byte[] key = LinuxSettings.GetOrCreateKey();
            byte[] blob = File.ReadAllBytes(InstallSealPath);
            byte[]? plain = LinuxSettings.DecryptWithAad(key, blob, InstallSealAad);
            if (plain == null) return false;

            return string.Equals(Encoding.UTF8.GetString(plain), ComputeBinaryDigestPayload(exe), StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeBinaryDigestPayload(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return $"{Convert.ToHexString(SHA256.HashData(fs))}|{fs.Length}";
    }
}
