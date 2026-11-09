using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PCRemote.Core.Interfaces.Storage;

namespace PCRemote.Platform.Windows.Security;

[SupportedOSPlatform("windows")]
public class WindowsSecretStore : ISecretStore
{
    private static readonly SemaphoreSlim Lock = new(1, 1);

    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PCRemote",
        "secrets.dat");

    public async Task<string?> GetSecretAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await Lock.WaitAsync(cancellationToken);

        try
        {
            var secrets = await LoadSecretsAsync(cancellationToken);

            return secrets.TryGetValue(key, out var value)
                ? value
                : null;
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task SetSecretAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        await Lock.WaitAsync(cancellationToken);

        try
        {
            var secrets = await LoadSecretsAsync(cancellationToken);

            secrets[key] = value;

            await SaveSecretsAsync(secrets, cancellationToken);
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task DeleteSecretAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await Lock.WaitAsync(cancellationToken);

        try
        {
            var secrets = await LoadSecretsAsync(cancellationToken);

            if (secrets.Remove(key))
            {
                await SaveSecretsAsync(secrets, cancellationToken);
            }
        }
        finally
        {
            Lock.Release();
        }
    }

    private static async Task<Dictionary<string, string>> LoadSecretsAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(StoragePath))
            return new Dictionary<string, string>();

        var encryptedData = await File.ReadAllBytesAsync(
            StoragePath,
            cancellationToken);

        var decryptedData = ProtectedData.Unprotect(
            encryptedData,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(
            decryptedData);

        return secrets ?? new Dictionary<string, string>();
    }

    private static async Task SaveSecretsAsync(
        Dictionary<string, string> secrets,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(StoragePath)!;

        Directory.CreateDirectory(directory);

        if (secrets.Count == 0)
        {
            if (File.Exists(StoragePath))
                File.Delete(StoragePath);

            return;
        }

        var jsonData = JsonSerializer.SerializeToUtf8Bytes(secrets);

        var encryptedData = ProtectedData.Protect(
            jsonData,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        var temporaryPath = StoragePath + ".tmp";

        try
        {
            await File.WriteAllBytesAsync(
                temporaryPath,
                encryptedData,
                cancellationToken);

            File.Move(temporaryPath, StoragePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}