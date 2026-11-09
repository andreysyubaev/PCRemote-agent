using System.Diagnostics;
using PCRemote.Core.Interfaces.Storage;

namespace PCRemote.Platform.MacOS.Security;

public class MacSecretStore : ISecretStore
{
    private const string ServiceName = "PCRemote";

    public async Task<string?> GetSecretAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteSecurityCommandAsync(
            ["find-generic-password", "-s", ServiceName, "-a", key, "-w"],
            cancellationToken,
            ignoreExitCode: true);

        if (result.ExitCode != 0)
            return null;

        return result.Output.Trim();
    }

    public async Task SetSecretAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        await ExecuteSecurityCommandAsync(
            ["add-generic-password", "-U", "-s", ServiceName,
                "-a", key, "-w", value],
            cancellationToken);
    }

    public async Task DeleteSecretAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await ExecuteSecurityCommandAsync(
            ["delete-generic-password", "-s", ServiceName, "-a", key],
            cancellationToken,
            ignoreExitCode: true);
    }

    private static async Task<(string Output, int ExitCode)>
        ExecuteSecurityCommandAsync(
            string[] arguments,
            CancellationToken cancellationToken,
            bool ignoreExitCode = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/security",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(
            cancellationToken);

        var errorTask = process.StandardError.ReadToEndAsync(
            cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (!ignoreExitCode && process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Keychain operation failed: {error}");
        }

        return (output, process.ExitCode);
    }
}