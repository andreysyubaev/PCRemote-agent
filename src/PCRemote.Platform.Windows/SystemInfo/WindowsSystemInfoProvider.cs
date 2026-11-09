using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using PCRemote.Core.Interfaces;
using PCRemote.Core.Models;

namespace PCRemote.Platform.Windows.SystemInformation;

[SupportedOSPlatform("windows")]
public class WindowsSystemInfoProvider : ISystemInfoProvider
{
    public async Task<SystemInfo> GetSystemInfoAsync(
        CancellationToken cancellationToken = default)
    {
        string osVersion = GetWindowsVersion();

        string processor = GetProcessorName();

        var memoryStatus = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref memoryStatus))
        {
            throw new InvalidOperationException(
                "Failed to get memory information.");
        }

        long totalMemory = (long)memoryStatus.TotalPhysical;

        string graphicCard = await GetGraphicCardNameAsync(
            cancellationToken);

        string systemDrivePath = Path.GetPathRoot(
            Environment.SystemDirectory)!;

        var systemDisk = new DriveInfo(systemDrivePath);
        long freeDiskSpace = systemDisk.AvailableFreeSpace;

        return new SystemInfo
        {
            OperatingSystem = "Windows",
            OSVersion = osVersion,
            Processor = processor,
            TotalMemoryBytes = totalMemory,
            FreeDiskSpaceBytes = freeDiskSpace,
            GraphicCard = graphicCard
        };
    }

    private static string GetWindowsVersion()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

        if (key == null)
        {
            return Environment.OSVersion.VersionString;
        }

        string productName =
            key.GetValue("ProductName")?.ToString() ?? "Windows";

        string displayVersion =
            key.GetValue("DisplayVersion")?.ToString() ?? "";

        return string.IsNullOrEmpty(displayVersion)
            ? productName
            : $"{productName} {displayVersion}";
    }

    private static string GetProcessorName()
    {
        object? value = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
            "ProcessorNameString",
            null);

        return value?.ToString()?.Trim() ?? "Unknown";
    }

    private static async Task<string> GetGraphicCardNameAsync(
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(
            "Get-CimInstance -ClassName Win32_VideoController | " +
            "Select-Object -ExpandProperty Name");

        using var process = new Process();
        process.StartInfo = startInfo;

        process.Start();

        Task<string> outputTask =
            process.StandardOutput.ReadToEndAsync();

        Task<string> errorTask =
            process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        string output = await outputTask;
        string error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to get graphics card information: {error}");
        }

        return string.IsNullOrWhiteSpace(output)
            ? "Unknown"
            : output.Trim();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(
        ref MemoryStatusEx buffer);
}