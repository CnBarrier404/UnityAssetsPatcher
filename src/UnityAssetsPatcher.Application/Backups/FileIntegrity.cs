using System.Security.Cryptography;

namespace UnityAssetsPatcher.Application.Backups;

public sealed record FileIntegrity(long Length, string Sha256)
{
    public static FileIntegrity Create(ReadOnlySpan<byte> contents)
    {
        return new FileIntegrity(contents.Length, Convert.ToHexStringLower(SHA256.HashData(contents)));
    }

    public static FileIntegrity Create(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long length = stream.Length;
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(stream));

        return new FileIntegrity(length, sha256);
    }

    public bool Matches(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var info = new FileInfo(path);

        if (info.Length != Length)
        {
            return false;
        }

        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        string actual = Convert.ToHexStringLower(SHA256.HashData(stream));

        return string.Equals(actual, Sha256, StringComparison.Ordinal);
    }
}
