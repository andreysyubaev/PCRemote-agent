using PCRemote.Core.Models;

namespace PCRemote.Core.Interfaces;

public interface ISystemInfoProvider
{
    Task<SystemInfo> GetSystemInfoAsync(
        CancellationToken cancellationToken = default);
}