using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace AudioSwitch.AudioCore;

internal sealed record InstallOptions(
    bool CreateStartMenuShortcut,
    bool CreateDesktopShortcut,
    bool LaunchOnStartup,
    bool LaunchAfterInstall);

internal static class InstallerService
{
    public const string AppDisplayName = "Audio Switch";
    public const string AppVersion = "1.3.0";
    public const string PublisherName = "Deshan Fernando";
    public const string RepoUrl = "https://github.com/deshanFdo/bluetooth-AUX";

    private static readonly byte[] InstallSealEntropy =
        Encoding.UTF8.GetBytes("AudioSwitch::InstallSeal::v1");

    private const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AudioSwitch";
    private const string RunRegKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "AudioSwitch";

    public static string DefaultInstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AudioSwitch");

    public static string SettingsDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioSwitch");

    public static string InstallSealPath => Path.Combine(SettingsDataDir, "install.sig");

    public static string DefaultExePath => Path.Combine(DefaultInstallDir, "AudioSwitch.exe");

    public static string StartMenuShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Audio Switch.lnk");

    public static string DesktopShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Audio Switch.lnk");

    public static bool IsAlreadyInstalled() => File.Exists(DefaultExePath);

    public static bool IsLaunchedAsSetup(string[] args)
    {
        if (args.Any(a => a.Equals("--install", StringComparison.OrdinalIgnoreCase)
                       || a.Equals("--setup", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string? procPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(procPath))
        {
            string fileName = Path.GetFileNameWithoutExtension(procPath);
            if (fileName.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                || fileName.Contains("Installer", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static async Task InstallAsync(InstallOptions options, IProgress<(int Percent, string Step)>? progress = null)
    {
        string sourceExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot locate running executable path.");

        progress?.Report((15, "Stopping any running Audio Switch instance..."));
        await StopOtherAudioSwitchInstancesAsync();

        progress?.Report((35, "Copying application to LocalAppData..."));
        Directory.CreateDirectory(DefaultInstallDir);

        string targetExe = DefaultExePath;
        if (!string.Equals(Path.GetFullPath(sourceExe), Path.GetFullPath(targetExe), StringComparison.OrdinalIgnoreCase))
        {
            await Task.Run(() =>
            {
                ClearReadOnly(targetExe);
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    try
                    {
                        File.Copy(sourceExe, targetExe, overwrite: true);
                        return;
                    }
                    catch (IOException) when (attempt < 9)
                    {
                        Thread.Sleep(250);
                    }
                }
                File.Copy(sourceExe, targetExe, overwrite: true);
            });
        }

        SealInstalledBinary(targetExe);

        progress?.Report((65, "Creating shortcuts..."));
        if (options.CreateStartMenuShortcut)
        {
            CreateShortcut(StartMenuShortcutPath, targetExe, DefaultInstallDir, "Dynamic Island Audio Switcher for Windows");
        }
        else if (File.Exists(StartMenuShortcutPath))
        {
            File.Delete(StartMenuShortcutPath);
        }

        if (options.CreateDesktopShortcut)
        {
            CreateShortcut(DesktopShortcutPath, targetExe, DefaultInstallDir, "Dynamic Island Audio Switcher for Windows");
        }
        else if (File.Exists(DesktopShortcutPath))
        {
            File.Delete(DesktopShortcutPath);
        }

        progress?.Report((82, "Registering Windows Apps & Startup..."));
        long sizeKb = Math.Max(1024, new FileInfo(targetExe).Length / 1024);
        using (var uninstallKey = Registry.CurrentUser.CreateSubKey(UninstallRegKey))
        {
            uninstallKey.SetValue("DisplayName", AppDisplayName);
            uninstallKey.SetValue("DisplayVersion", AppVersion);
            uninstallKey.SetValue("Publisher", PublisherName);
            uninstallKey.SetValue("DisplayIcon", targetExe);
            uninstallKey.SetValue("InstallLocation", DefaultInstallDir);
            uninstallKey.SetValue("UninstallString", $"\"{targetExe}\" --uninstall");
            uninstallKey.SetValue("QuietUninstallString", $"\"{targetExe}\" --uninstall --silent");
            uninstallKey.SetValue("URLInfoAbout", RepoUrl);
            uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
            uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            uninstallKey.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, sizeKb), RegistryValueKind.DWord);
        }

        using (var runKey = Registry.CurrentUser.CreateSubKey(RunRegKey))
        {
            if (options.LaunchOnStartup)
            {
                runKey.SetValue(RunValueName, $"\"{targetExe}\"");
            }
            else
            {
                runKey.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }

        progress?.Report((100, "Installation complete!"));
    }

    public static async Task UninstallAsync(bool scheduleDeferredSelfDelete = true)
    {
        await StopOtherAudioSwitchInstancesAsync();

        try { if (File.Exists(StartMenuShortcutPath)) File.Delete(StartMenuShortcutPath); } catch { }
        try { if (File.Exists(DesktopShortcutPath)) File.Delete(DesktopShortcutPath); } catch { }

        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunRegKey, writable: true);
            runKey?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch { }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(UninstallRegKey, throwOnMissingSubKey: false);
        }
        catch { }

        // Remove saved settings folder (%LocalAppData%\AudioSwitch)
        try
        {
            if (Directory.Exists(SettingsDataDir))
            {
                ClearReadOnlyRecursive(SettingsDataDir);
                Directory.Delete(SettingsDataDir, recursive: true);
            }
        }
        catch { }

        string installDir = Path.GetFullPath(DefaultInstallDir).TrimEnd('\\');
        ClearReadOnlyRecursive(installDir);
        if (IsRunningFromInstallDir())
        {
            // Release CWD handle immediately so cmd.exe rmdir can delete the directory cleanly after exit
            try { Environment.CurrentDirectory = Path.GetTempPath(); } catch { }

            // Delete any extra files inside installDir right away
            DeleteNonLockedFilesInDirectory(installDir);

            if (scheduleDeferredSelfDelete)
            {
                ScheduleSelfDeleteIfRunningFromInstallDir();
            }
        }
        else if (Directory.Exists(installDir))
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    Directory.Delete(installDir, recursive: true);
                    break;
                }
                catch (IOException) when (attempt < 7)
                {
                    await Task.Delay(250);
                }
                catch
                {
                    break;
                }
            }
            CleanupSingleFileExtractionCache();
        }
    }

    public static bool IsRunningFromInstallDir()
    {
        string installDir = Path.GetFullPath(DefaultInstallDir).TrimEnd('\\') + Path.DirectorySeparatorChar;
        string? curExe = Environment.ProcessPath;
        return !string.IsNullOrWhiteSpace(curExe)
            && Path.GetFullPath(curExe).StartsWith(installDir, StringComparison.OrdinalIgnoreCase);
    }

    public static void ScheduleSelfDeleteIfRunningFromInstallDir()
    {
        if (!IsRunningFromInstallDir()) return;

        string installDir = Path.GetFullPath(DefaultInstallDir).TrimEnd('\\');
        string settingsDir = Path.GetFullPath(SettingsDataDir).TrimEnd('\\');
        string bundleCache = Path.Combine(Path.GetTempPath(), ".net", "AudioSwitch").TrimEnd('\\');

        static bool IsSafeCmdPath(string p) =>
            !string.IsNullOrWhiteSpace(p)
            && p.IndexOfAny(['"', '&', '|', '>', '<', '^', '%', '\r', '\n']) < 0;

        if (!IsSafeCmdPath(installDir) || !IsSafeCmdPath(settingsDir) || !IsSafeCmdPath(bundleCache))
        {
            return;
        }

        string batchPath = Path.Combine(Path.GetTempPath(), $"audioswitch_cleanup_{Guid.NewGuid():N}.cmd");
        try { Environment.CurrentDirectory = Path.GetTempPath(); } catch { }

        string script = string.Join("\r\n",
        [
            "@echo off",
            "for /L %%i in (1,1,25) do (",
            $"  if exist \"{settingsDir}\" (attrib -r -s -h \"{settingsDir}\\*.*\" /s /d >nul 2>&1 & rmdir /s /q \"{settingsDir}\" >nul 2>&1)",
            $"  if exist \"{installDir}\" (attrib -r -s -h \"{installDir}\\*.*\" /s /d >nul 2>&1 & rmdir /s /q \"{installDir}\" >nul 2>&1)",
            $"  if not exist \"{installDir}\" if not exist \"{settingsDir}\" goto :done",
            "  ping -n 2 127.0.0.1 >nul",
            ")",
            ":done",
            "ping -n 2 127.0.0.1 >nul",
            $"if exist \"{bundleCache}\" rmdir /s /q \"{bundleCache}\" >nul 2>&1",
            "del /f /q \"%~f0\" >nul 2>&1"
        ]);
        File.WriteAllText(batchPath, script, Encoding.ASCII);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{batchPath}\"\"",
            WorkingDirectory = Path.GetTempPath(),
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    private static void DeleteNonLockedFilesInDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;
        string? selfExe = Environment.ProcessPath != null ? Path.GetFullPath(Environment.ProcessPath) : null;
        try
        {
            foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                if (selfExe != null && string.Equals(Path.GetFullPath(file), selfExe, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                try { File.Delete(file); } catch { }
            }
        }
        catch { }
    }

    private static void CleanupSingleFileExtractionCache()
    {
        try
        {
            string bundleCache = Path.Combine(Path.GetTempPath(), ".net", "AudioSwitch");
            if (Directory.Exists(bundleCache))
            {
                Directory.Delete(bundleCache, recursive: true);
            }
        }
        catch { }
    }

    /// <summary>
    /// Seals the installed executable SHA-256 under Windows DPAPI and sets ReadOnly attributes
    /// so external edits after installation are blocked or detected at launch.
    /// </summary>
    public static void SealInstalledBinary(string exePath)
    {
        try
        {
            if (!File.Exists(exePath)) return;
            string payload = ComputeBinaryDigestPayload(exePath);
            byte[]? sealedBytes = IslandSettings.ProtectWithEntropy(
                Encoding.UTF8.GetBytes(payload), InstallSealEntropy);

            if (sealedBytes != null)
            {
                Directory.CreateDirectory(SettingsDataDir);
                ClearReadOnly(InstallSealPath);
                File.WriteAllBytes(InstallSealPath, sealedBytes);
                SetReadOnly(InstallSealPath);
            }

            SetReadOnly(exePath);
        }
        catch { }
    }

    public static bool VerifyInstalledBinaryIntegrity()
    {
        if (!IsRunningFromInstallDir()) return true;
        if (!File.Exists(InstallSealPath)) return true;

        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return true;

            byte[] sealedBytes = File.ReadAllBytes(InstallSealPath);
            byte[]? plain = IslandSettings.UnprotectWithEntropy(sealedBytes, InstallSealEntropy);
            if (plain == null) return false;

            string expected = Encoding.UTF8.GetString(plain);
            string actual = ComputeBinaryDigestPayload(exePath);
            return string.Equals(expected, actual, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeBinaryDigestPayload(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] hash = SHA256.HashData(fs);
        return $"{Convert.ToHexString(hash)}|{fs.Length}";
    }

    private static void ClearReadOnly(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var attrs = File.GetAttributes(path);
            if ((attrs & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
            }
        }
        catch { }
    }

    private static void SetReadOnly(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var attrs = File.GetAttributes(path);
            File.SetAttributes(path, attrs | FileAttributes.ReadOnly);
        }
        catch { }
    }

    private static void ClearReadOnlyRecursive(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                ClearReadOnly(f);
            }
        }
        catch { }
    }

    public static void LaunchInstalledApp()
    {
        if (!File.Exists(DefaultExePath)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = DefaultExePath,
            WorkingDirectory = DefaultInstallDir,
            UseShellExecute = true
        });
    }

    private static async Task StopOtherAudioSwitchInstancesAsync()
    {
        int myPid = Environment.ProcessId;
        string installDirPrefix = Path.GetFullPath(DefaultInstallDir).TrimEnd('\\') + Path.DirectorySeparatorChar;

        foreach (var proc in Process.GetProcesses())
        {
            if (proc.Id == myPid)
            {
                proc.Dispose();
                continue;
            }

            bool isAudioSwitch = false;
            try
            {
                if (proc.ProcessName.Equals("AudioSwitch", StringComparison.OrdinalIgnoreCase))
                {
                    isAudioSwitch = true;
                }
                else
                {
                    string? path = proc.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) &&
                        Path.GetFullPath(path).StartsWith(installDirPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        isAudioSwitch = true;
                    }
                }
            }
            catch
            {
                // Protected/system process
            }

            if (isAudioSwitch)
            {
                try
                {
                    proc.Kill(entireProcessTree: true);
                    await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));
                }
                catch { }
            }

            proc.Dispose();
        }
        await Task.Delay(300);
    }

    private static void CreateShortcut(string shortcutPath, string targetExe, string workingDir, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        var link = (IShellLinkW)new ShellLinkCoClass();
        link.SetPath(targetExe);
        link.SetWorkingDirectory(workingDir);
        link.SetDescription(description);
        link.SetIconLocation(targetExe, 0);

        var file = (IPersistFile)link;
        file.Save(shortcutPath, true);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
