using System.Diagnostics;
using PCRemote.Core.Interfaces;

namespace PCRemote.Platform.MacOS.Screenshots;

public class MacScreenshotService : IScreenshotService
{
    public async Task<Stream> CaptureAsync(
        CancellationToken cancellationToken = default)
    {
        string screenshotPath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.png");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/sbin/screencapture",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-x");
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add("png");
            startInfo.ArgumentList.Add(screenshotPath);

            using var process = new Process();
            process.StartInfo = startInfo;

            process.Start();

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
                    await process.WaitForExitAsync();
                }

                throw;
            }

            string error = await errorTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to capture screenshot: {error}");
            }

            if (!File.Exists(screenshotPath))
            {
                throw new InvalidOperationException(
                    "Screenshot file was not created.");
            }

            byte[] imageBytes = await File.ReadAllBytesAsync(
                screenshotPath,
                cancellationToken);

            return new MemoryStream(imageBytes);
        }
        finally
        {
            if (File.Exists(screenshotPath))
            {
                File.Delete(screenshotPath);
            }
        }
    }
}