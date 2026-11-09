using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using PCRemote.Core.Interfaces;

namespace PCRemote.Platform.MacOS.SystemInfo;

public class MacSystemInfoProvider : ISystemInfoProvider
{
    public async Task<Core.Models.SystemInfo> GetSystemInfoAsync(
        CancellationToken cancellationToken = default)
    {
        // Версия macOS
        string osVersion = await RunCommandAsync(
            "/usr/bin/sw_vers",
            cancellationToken,
            "-productVersion");

        // Модель процессора
        string processor = await RunCommandAsync(
            "/usr/sbin/sysctl",
            cancellationToken,
            "-n",
            "machdep.cpu.brand_string");

        // Объём оперативной памяти
        string memoryResult = await RunCommandAsync(
            "/usr/sbin/sysctl",
            cancellationToken,
            "-n",
            "hw.memsize");

        long totalMemory = long.Parse(
            memoryResult,
            CultureInfo.InvariantCulture);

        // Информация о видеокарте
        string graphicsJson = await RunCommandAsync(
            "/usr/sbin/system_profiler",
            cancellationToken,
            "-json",
            "SPDisplaysDataType");

        string graphicCard = GetGraphicCardName(graphicsJson);

        // Свободное место на системном диске
        var systemDisk = new DriveInfo("/");
        long freeDiskSpace = systemDisk.AvailableFreeSpace;

        return new Core.Models.SystemInfo
        {
            OperatingSystem = "macOS",
            OSVersion = osVersion,
            Processor = processor,
            TotalMemoryBytes = totalMemory,
            FreeDiskSpaceBytes = freeDiskSpace,
            GraphicCard = graphicCard
        };
    }

    private static async Task<string> RunCommandAsync(
        string fileName,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process();

        process.StartInfo = startInfo;

        process.Start();

        Task<string> outputTask =
            process.StandardOutput.ReadToEndAsync(cancellationToken);

        Task<string> errorTask =
            process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        string output = await outputTask;
        string error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command failed: {fileName}. {error}");
        }

        return output.Trim();
    }

    private static string GetGraphicCardName(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty(
                "SPDisplaysDataType",
                out JsonElement displays))
        {
            return "Unknown";
        }

        foreach (JsonElement display in displays.EnumerateArray())
        {
            string[] propertyNames =
            {
                "sppci_model",
                "spdisplays_chipset_model"
            };

            foreach (string propertyName in propertyNames)
            {
                if (display.TryGetProperty(
                        propertyName,
                        out JsonElement value))
                {
                    return value.GetString() ?? "Unknown";
                }
            }
        }

        return "Unknown";
    }
}