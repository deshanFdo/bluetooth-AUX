using System.Diagnostics;

namespace AudioSwitch.Linux.LinuxCore;

internal static class LinuxInstallerService
{
    public static string UserInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AudioSwitch");

    public static string DesktopEntryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "applications",
        "audioswitch.desktop");

    public static string AutostartEntryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "autostart",
        "audioswitch.desktop");

    public static void InstallUserLocal(bool autostart = true)
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) return;

        Directory.CreateDirectory(UserInstallDir);
        string targetExe = Path.Combine(UserInstallDir, "AudioSwitch");
        if (!string.Equals(Path.GetFullPath(exe), Path.GetFullPath(targetExe), StringComparison.Ordinal))
        {
            File.Copy(exe, targetExe, overwrite: true);
            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(targetExe,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }

        string desktopContent = $"""
            [Desktop Entry]
            Type=Application
            Name=Audio Switch
            Comment=Dynamic Island Audio Switcher, Split Audio & Spotify / YouTube Music Control
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
        try { if (Directory.Exists(LinuxSettings.ConfigDir)) Directory.Delete(LinuxSettings.ConfigDir, recursive: true); } catch { }

        bool runningSystemDeb = File.Exists("/usr/lib/audioswitch/AudioSwitch");
        string installDir = UserInstallDir;
        string? curExe = Environment.ProcessPath;

        if (Directory.Exists(installDir))
        {
            if (curExe != null && curExe.StartsWith(installDir, StringComparison.Ordinal))
            {
                // Spawn detached sh cleanup after exit
                Process.Start(new ProcessStartInfo("sh", $"-c \"sleep 1; rm -rf '{installDir}' '{LinuxSettings.ConfigDir}'\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            else
            {
                try { Directory.Delete(installDir, recursive: true); } catch { }
            }
        }

        if (runningSystemDeb && LinuxCommandRunner.CommandExists("pkexec"))
        {
            LinuxCommandRunner.StartDetached("pkexec", ["apt-get", "purge", "-y", "audioswitch"]);
        }
    }
}
