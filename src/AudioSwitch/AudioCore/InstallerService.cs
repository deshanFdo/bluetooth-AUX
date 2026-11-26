using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
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
    public const string AppVersion = "1.0.0";
    public const string PublisherName = "Deshan Fernando";
    public const string RepoUrl = "https://github.com/deshanFdo/bluetooth-AUX";

    private const string UninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AudioSwitch";
    private const string RunRegKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "AudioSwitch";

    public static string DefaultInstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AudioSwitch");

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

    public static async Task UninstallAsync()
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

        string installDir = DefaultInstallDir;
        string? curExe = Environment.ProcessPath;
        bool runningFromInstallDir = !string.IsNullOrWhiteSpace(curExe)
            && Path.GetFullPath(curExe).StartsWith(Path.GetFullPath(installDir), StringComparison.OrdinalIgnoreCase);

        if (runningFromInstallDir)
        {
            // Schedule deferred cleanup after this process exits cleanly
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{installDir}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        else if (Directory.Exists(installDir))
        {
            try { Directory.Delete(installDir, recursive: true); } catch { }
        }
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
        foreach (var proc in Process.GetProcessesByName("AudioSwitch"))
        {
            if (proc.Id == myPid) continue;
            try
            {
                proc.Kill(entireProcessTree: true);
                await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));
            }
            catch
            {
                // Best effort
            }
            finally
            {
                proc.Dispose();
            }
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
