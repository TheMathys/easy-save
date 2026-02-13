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
