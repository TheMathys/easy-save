using System.Threading;
using System.Threading.Tasks;

namespace EasySave.Core.Interfaces;

/// <summary>
/// Encrypts a file using an external process (e.g. CryptoSoft).
/// Follows Strategy pattern: the backup executor can use this instead of a plain copy
/// for files whose extension is in the encryption list.
/// </summary>
public interface IFileEncryptor
{
    /// <summary>
    /// Encrypts the source file to the destination path using the given key file,
    /// by launching the external encryption executable.
    /// </summary>
    /// <param name="sourcePath">Absolute path of the source file.</param>
    /// <param name="destinationPath">Absolute path of the output (encrypted) file.</param>
    /// <param name="keyFilePath">Absolute path of the encryption key file.</param>
    /// <param name="cryptoSoftExecutablePath">Absolute path to the CryptoSoft executable.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// Time taken in milliseconds on success, or a negative value on failure (e.g. -4).
    /// </returns>
    Task<long> EncryptFileAsync(
        string sourcePath,
        string destinationPath,
        string keyFilePath,
        string cryptoSoftExecutablePath,
        CancellationToken cancellationToken = default);
}
