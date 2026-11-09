namespace PCRemote.Core.Interfaces;

public interface IScreenshotService
{
    Task<Stream> CaptureAsync(
        CancellationToken cancellationToken = default);
}