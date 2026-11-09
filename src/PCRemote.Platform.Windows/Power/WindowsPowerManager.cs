using System.Diagnostics;
using System.Runtime.InteropServices;
using PCRemote.Core.Interfaces;

namespace PCRemote.Platform.Windows.Power;

public class WindowsPowerManager : IPowerManager
{
    public Task ShutdownAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteCommandAsync(
            "/s", "/t", "0",
            cancellationToken);
    }

    public Task RestartAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteCommandAsync(
            "/r", "/t", "0",
            cancellationToken);
    }

    public Task SleepAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool success = SetSuspendState(
            false,
            false,
            false);

        if (!success)
            throw new InvalidOperationException(
                "Failed to put Windows into sleep mode.");

        return Task.CompletedTask;
    }

    private static async Task ExecuteCommandAsync(
        string firstArgument,
        string secondArgument,
        string thirdArgument,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add(firstArgument);
        startInfo.ArgumentList.Add(secondArgument);
        startInfo.ArgumentList.Add(thirdArgument);

        using var process = new Process();
        process.StartInfo = startInfo;

        process.Start();

        string error = await process.StandardError.ReadToEndAsync(
            cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Windows power command failed: {error}");
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(
        bool hibernate,
        bool forceCritical,
        bool disableWakeEvent);
}