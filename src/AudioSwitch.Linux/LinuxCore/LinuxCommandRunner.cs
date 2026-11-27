using System.Diagnostics;

namespace AudioSwitch.Linux.LinuxCore;

internal static class LinuxCommandRunner
{
    public static (int ExitCode, string StdOut, string StdErr) Run(
        string fileName,
        IEnumerable<string> args,
        int timeoutMs = 4000)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (string arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var proc = Process.Start(psi);
            if (proc == null) return (-1, string.Empty, "Failed to start process.");

            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(timeoutMs))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return (-2, stdout, "Timed out.");
            }
            return (proc.ExitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }

    public static Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string fileName,
        IEnumerable<string> args,
        int timeoutMs = 4000)
        => Task.Run(() => Run(fileName, args, timeoutMs));

    public static bool CommandExists(string commandName)
    {
        var (code, _, _) = Run("which", [commandName], 1500);
        return code == 0;
    }

    public static void StartDetached(string fileName, IEnumerable<string> args)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (string arg in args)
            {
                psi.ArgumentList.Add(arg);
            }
            Process.Start(psi);
        }
        catch { }
    }
}
