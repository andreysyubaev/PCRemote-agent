using System;
using System.Runtime.Versioning;
using Microsoft.Win32;
using PCRemote.Core.Interfaces;

namespace PCRemote.Platform.Windows.Startup;

[SupportedOSPlatform("windows")]
public class WindowsAutoStartService : IAutoStartService
{
    private const string ApplicationName = "PCRemote";

    private const string RunRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            RunRegistryPath);

        return key?.GetValue(ApplicationName) is not null;
    }

    public void Enable()
    {
        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException(
                "Unable to determine PCRemote executable path.");
        }

        using var key = Registry.CurrentUser.CreateSubKey(
            RunRegistryPath);

        key.SetValue(
            ApplicationName,
            $"\"{executablePath}\" --minimized",
            RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            RunRegistryPath,
            writable: true);

        key?.DeleteValue(
            ApplicationName,
            throwOnMissingValue: false);
    }
}