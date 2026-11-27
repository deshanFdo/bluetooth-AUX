using Avalonia;
using AudioSwitch.Linux.LinuxCore;

namespace AudioSwitch.Linux;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(a => a.Equals("--diag", StringComparison.OrdinalIgnoreCase)))
        {
            RunDiagnostics();
            return 0;
        }

        if (args.Any(a => a.Equals("--install", StringComparison.OrdinalIgnoreCase)))
        {
            LinuxInstallerService.InstallUserLocal(autostart: true);
            Console.WriteLine($"Installed Audio Switch to {LinuxInstallerService.UserInstallDir}");
            return 0;
        }

        if (args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            LinuxInstallerService.UninstallAll();
            Console.WriteLine("Audio Switch uninstalled cleanly.");
            return 0;
        }

        if (!LinuxInstallerService.VerifyInstalledIntegrity())
        {
            Console.Error.WriteLine("Audio Switch integrity check failed: installed binary was modified outside the installer.");
            return 1;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void RunDiagnostics()
    {
        Console.WriteLine($"Audio Switch Linux v1.3.0 | Session={Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unknown"}");

        var audio = new PipeWireAudioService();
        var sinks = audio.GetSinks();
        Console.WriteLine($"Detected {sinks.Count} PipeWire/PulseAudio sinks (outputs):");
        foreach (var s in sinks)
        {
            Console.WriteLine($"  [{s.Category}] {s.FriendlyName} ({s.Name}) | Default={s.IsDefault} | Muted={s.IsMuted} | Vol={s.VolumePercent}%");
        }

        var sources = audio.GetSources();
        Console.WriteLine($"Detected {sources.Count} capture sources (mics):");
        foreach (var src in sources)
        {
            Console.WriteLine($"  [{src.Category}] {src.FriendlyName} ({src.Name}) | Default={src.IsDefault} | Muted={src.IsMuted} | Vol={src.VolumePercent}%");
        }

        var bats = LinuxTelemetryService.ReadConnectedBluetoothBatteries();
        Console.WriteLine($"Detected {bats.Count} connected Bluetooth batteries:");
        foreach (var b in bats)
        {
            Console.WriteLine($"  {b.DeviceName}: {b.Percent}%");
        }

        using var mpris = new MprisMediaService();
        var buses = mpris.ListMprisBusNames();
        Console.WriteLine($"Detected {buses.Count} active MPRIS players: {string.Join(", ", buses)}");
    }
}
