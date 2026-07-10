using System.Security.Cryptography;
using System.Text;

namespace UnityAssetsPatcher.Application.Backups;

public static class GameInstanceIdentity
{
    public static string CreateFingerprint(string gameDirectory)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizePath(gameDirectory)));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizePath(string gameDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);

        string fullPath = Path.GetFullPath(gameDirectory);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Game directory not found: {fullPath}");
        }

        string root = Path.GetPathRoot(fullPath)
                      ?? throw new InvalidOperationException($"Cannot resolve game directory: {gameDirectory}");
        string resolved = root;
        foreach (string segment in fullPath[root.Length..].Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            resolved = Path.Combine(resolved, segment);
            var info = new DirectoryInfo(resolved);

            if (info.LinkTarget is not null)
            {
                resolved = info.ResolveLinkTarget(true)?.FullName
                           ?? throw new InvalidOperationException($"Cannot resolve game directory: {gameDirectory}");
            }
        }

        resolved = Path.TrimEndingDirectorySeparator(Path.GetFullPath(resolved));

        return OperatingSystem.IsWindows() ? resolved.ToUpperInvariant() : resolved;
    }
}
