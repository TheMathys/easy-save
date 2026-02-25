using System.Diagnostics;
using EasySave.Core.Interfaces;

namespace EasySave.Infrastructure.Encryption;

/// <summary>
/// Adapter for the external CryptoSoft executable: encrypts a file by launching a process
/// with arguments (source path, destination path, key file path). Exit code: -1 invalid args,
/// -4 failure, otherwise milliseconds on success.
/// </summary>
public sealed class CryptoSoftFileEncryptor : IFileEncryptor
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;
    
    /// <inheritdoc />
    public async Task<long> EncryptFileAsync(
        string sourcePath, 
        string destinationPath, 
        string keyFilePath, 
        string cryptoSoftExecutablePath, 
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(cryptoSoftExecutablePath);

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            if (await _semaphore.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
            {
                try
                {
                    return await ExecuteCryptoSoft(sourcePath, destinationPath, keyFilePath, cryptoSoftExecutablePath, cancellationToken);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            else
            {
                await Task.Delay(RetryDelayMs * (attempt + 1), cancellationToken);
            }
        }
        throw new InvalidOperationException("Échec après tous les retries : CryptoSoft inaccessible.");
    }
    
    private async Task<long> ExecuteCryptoSoft(
        string sourcePath, 
        string destinationPath, 
        string keyFilePath, 
        string cryptoSoftExecutablePath, 
        CancellationToken cancellationToken = default)
    {
        string? exeDir = Path.GetDirectoryName(Path.GetFullPath(cryptoSoftExecutablePath));
        var startInfo = new ProcessStartInfo
        {
            FileName = cryptoSoftExecutablePath,
            Arguments = $"\"{sourcePath}\" \"{destinationPath}\" \"{keyFilePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrEmpty(exeDir) ? "." : exeDir
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return process.ExitCode;
    }
}
