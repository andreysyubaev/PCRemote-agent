using System.Diagnostics;
using PCRemote.Core.Interfaces;

namespace PCRemote.Platform.Windows.Screenshots;

public class WindowsScreenshotService : IScreenshotService
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
                FileName = "powershell.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");

            string script =
                "Add-Type -AssemblyName System.Windows.Forms; " +
                "Add-Type -AssemblyName System.Drawing; " +
                "$bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen; " +
                "$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height); " +
                "$graphics = [System.Drawing.Graphics]::FromImage($bitmap); " +
                "try { " +
                "$graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $bitmap.Size); " +
                $"$bitmap.Save('{screenshotPath.Replace("'", "''")}', [System.Drawing.Imaging.ImageFormat]::Png); " +
                "} finally { " +
                "$graphics.Dispose(); " +
                "$bitmap.Dispose(); " +
                "}";

            startInfo.ArgumentList.Add(script);

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
                throw new InvalidOperationException(
                    $"Failed to capture screenshot: {error}");

            if (!File.Exists(screenshotPath))
                throw new InvalidOperationException(
                    "Screenshot file was not created.");

            byte[] imageBytes = await File.ReadAllBytesAsync(
                screenshotPath,
                cancellationToken);

            return new MemoryStream(imageBytes);
        }
        finally
        {
            if (File.Exists(screenshotPath))
                File.Delete(screenshotPath);
        }
    }
}