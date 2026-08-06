using System.Security.Cryptography;
using System.Text;
using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Application.Repository;

public static class GameInstanceIdentity
{
    public static string CreateFingerprint(IFileSystemOperations fileSystemOperations, string gameDirectory)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);

        return CreateFingerprint(new TrustedPathResolver(fileSystemOperations), gameDirectory);
    }

    public static string CreateFingerprint(TrustedPathResolver pathResolver, string gameDirectory)
    {
        ArgumentNullException.ThrowIfNull(pathResolver);

        string resolvedPath = pathResolver.ResolveExistingDirectory(gameDirectory);
        string identity = OperatingSystem.IsWindows() ? resolvedPath.ToUpperInvariant() : resolvedPath;
        byte[] bytes = Encoding.UTF8.GetBytes(identity);
        byte[] digest = SHA256.HashData(bytes);

        return Convert.ToHexStringLower(digest);
    }
}
