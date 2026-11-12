using PCRemote.Core.Interfaces.Storage;

namespace PCRemote.Tests.TestDoubles;

public class FakeSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = new();

    public Task<string?> GetSecretAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _secrets.TryGetValue(key, out var value);

        return Task.FromResult(value);
    }

    public Task SetSecretAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _secrets[key] = value;

        return Task.CompletedTask;
    }

    public Task DeleteSecretAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _secrets.Remove(key);

        return Task.CompletedTask;
    }
}