using System.Security.Cryptography;
using System.Text;
using UnityAssetsPatcher.Abstractions.IO;

namespace UnityAssetsPatcher.Application.Backups;

public static class GameInstanceIdentity
{
    public static string CreateFingerprint(
        IFileSystemOperations fileSystemOperations,
        string gameDirectory)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        string resolved = fileSystemOperations.ResolveExistingDirectory(gameDirectory);
        string identity = OperatingSystem.IsWindows() ? resolved.ToUpperInvariant() : resolved;
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
