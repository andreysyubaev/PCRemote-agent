using System.Diagnostics;
using PCRemote.Core.Interfaces;

namespace PCRemote.Platform.MacOS.Power;

public class MacPowerManager : IPowerManager
{
    public Task ShutdownAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteAppleScriptAsync(
            "tell application \"System Events\" to shut down",
            cancellationToken);
    }

    public Task RestartAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteAppleScriptAsync(
            "tell application \"System Events\" to restart",
            cancellationToken);
    }

    public Task SleepAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteAppleScriptAsync(
            "tell application \"System Events\" to sleep",
            cancellationToken);
    }

    private static async Task ExecuteAppleScriptAsync(
        string script,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/osascript",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add(script);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        string error = await process.StandardError.ReadToEndAsync(
            cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"macOS power command failed: {error}");
        }
    }
}