namespace PCRemote.Core.Interfaces.Storage;

public interface ISecretStore
{
    Task<string?> GetSecretAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task SetSecretAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default);

    Task DeleteSecretAsync(
        string key,
        CancellationToken cancellationToken = default);
}