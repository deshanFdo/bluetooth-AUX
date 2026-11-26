using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using AudioSwitch.AudioCore;

namespace AudioSwitch;

public partial class App : System.Windows.Application
{
    private const string MutexName = "AudioSwitch_SingleInstance_Mutex_v2";
    private const string WakePipeName = "AudioSwitch_WakeWindow_Pipe_v2";

    private Mutex? _instanceMutex;
    private CancellationTokenSource? _wakeCts;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--diag", StringComparer.OrdinalIgnoreCase))
        {
            AttachConsole(-1);
            RunDiagnostics();
            Shutdown(0);
            return;
        }

        _instanceMutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", WakePipeName, PipeDirection.Out);
                client.Connect(400);
            }
            catch
            {
                // Ignore if existing instance is closing
            }

            Shutdown(0);
            return;
        }

        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        StartWakeListener(window);
    }

    private void StartWakeListener(MainWindow window)
    {
        _wakeCts = new CancellationTokenSource();
        var token = _wakeCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        WakePipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);

                    _ = Dispatcher.BeginInvoke(() =>
                    {
                        if (window.WindowState == WindowState.Minimized)
                        {
                            window.WindowState = WindowState.Normal;
                        }
                        window.Show();
                        window.Activate();
                        window.ExpandIslandFromWake();
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(250, token);
                }
            }
        }, token);
    }

    private static void RunDiagnostics()
    {
        var policy = new PolicyConfigClient();
        Console.WriteLine($"PolicyConfig COM interfaces: {policy.ProbeSupportedInterface()}");
        using var router = new AudioRouterService();

        var renders = router.GetRenderEndpoints();
        Console.WriteLine($"Detected {renders.Count} render (output) endpoints:");
        foreach (var ep in renders)
        {
            Console.WriteLine(
                $"  [{ep.Category}] {ep.FriendlyName} | State={ep.State} | Default={ep.IsDefaultPlayback} | " +
                $"Muted={ep.IsMuted} | Vol={ep.VolumePercent}% | Enum={ep.EnumeratorName} | FF={ep.FormFactor}");
        }

        var captures = router.GetCaptureEndpoints();
        Console.WriteLine($"Detected {captures.Count} capture (mic) endpoints:");
        foreach (var ep in captures)
        {
            Console.WriteLine(
                $"  [{ep.Category}] {ep.FriendlyName} | State={ep.State} | Default={ep.IsDefaultPlayback} | " +
                $"Muted={ep.IsMuted} | Vol={ep.VolumePercent}% | Enum={ep.EnumeratorName} | FF={ep.FormFactor}");
        }

        var btBats = SystemStatusService.ReadBluetoothBatteries();
        Console.WriteLine($"Detected {btBats.Count} Bluetooth batteries:");
        foreach (var b in btBats)
        {
            Console.WriteLine($"  {b.DeviceName}: {b.Percent}%");
        }

        Console.Out.Flush();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _wakeCts?.Cancel();
        _wakeCts?.Dispose();
        try { _instanceMutex?.ReleaseMutex(); } catch { }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
