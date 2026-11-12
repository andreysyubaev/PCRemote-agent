using PCRemote.Core.Interfaces;
using PCRemote.Core.Models;

namespace PCRemote.Tests.TestDoubles;

public sealed class TrackingSystemInfoProvider : ISystemInfoProvider
{
    public int GetSystemInfoCallCount { get; private set; }

    public Task<SystemInfo> GetSystemInfoAsync(
        CancellationToken cancellationToken = default)
    {
        GetSystemInfoCallCount++;

        return Task.FromResult(new SystemInfo());
    }
}