
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PCRemote.Core.Interfaces;

namespace PCRemote.Platform.MacOS.Startup;

public class MacAutoStartService : IAutoStartService
{
    private const string LaunchAgentName = "com.pcremote.desktop";

    private static string LaunchAgentsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "LaunchAgents");

    private static string PlistPath =>
        Path.Combine(
            LaunchAgentsDirectory,
            $"{LaunchAgentName}.plist");

    private static string LaunchDomain =>
        $"gui/{GetCurrentUserId()}";

    [DllImport("libc")]
    private static extern uint geteuid();

    private static uint GetCurrentUserId()
    {
        return geteuid();
    }

    public bool IsEnabled()
    {
        return File.Exists(PlistPath);
    }

    public void Enable()
    {
        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException(
                "Unable to determine PCRemote executable path.");
        }

        Directory.CreateDirectory(LaunchAgentsDirectory);

        var document = new XDocument(
            new XElement("plist",
                new XAttribute("version", "1.0"),
                new XElement("dict",

                    new XElement("key", "Label"),
                    new XElement("string", LaunchAgentName),

                    new XElement("key", "ProgramArguments"),
                    new XElement("array",
                        new XElement("string", executablePath),
                        new XElement("string", "--minimized")),

                    new XElement("key", "RunAtLoad"),
                    new XElement("true")
                )
            )
        );

        document.Save(PlistPath);

        // Если LaunchAgent уже загружен, сначала выгружаем его.
        if (IsLaunchAgentLoaded())
        {
            RunLaunchctl("bootout", $"{LaunchDomain}/{LaunchAgentName}");
        }

        // Регистрируем LaunchAgent в текущей сессии macOS.
        var result = RunLaunchctl(
            "bootstrap",
            LaunchDomain,
            PlistPath);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Unable to register LaunchAgent: {result.Error}");
        }
    }

    public void Disable()
    {
        // Сначала выгружаем LaunchAgent.
        if (IsLaunchAgentLoaded())
        {
            var result = RunLaunchctl(
                "bootout",
                $"{LaunchDomain}/{LaunchAgentName}");

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Unable to unregister LaunchAgent: {result.Error}");
            }
        }

        // После успешной выгрузки удаляем файл.
        if (File.Exists(PlistPath))
        {
            File.Delete(PlistPath);
        }
    }

    private static bool IsLaunchAgentLoaded()
    {
        var result = RunLaunchctl(
            "print",
            $"{LaunchDomain}/{LaunchAgentName}");

        return result.ExitCode == 0;
    }

    private static (int ExitCode, string Output, string Error)
        RunLaunchctl(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/launchctl",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Unable to start launchctl.");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        return (
            process.ExitCode,
            outputTask.GetAwaiter().GetResult(),
            errorTask.GetAwaiter().GetResult()
        );
    }
}
