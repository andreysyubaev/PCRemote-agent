using PCRemote.Core.Models.Logging;

namespace PCRemote.Core.Interfaces.Logging;

public interface ILogReader
{
    Task<IReadOnlyList<LogEntry>> ReadAllAsync(
        CancellationToken cancellationToken = default);
}