using System.Security.Cryptography;

namespace UnityAssetsPatcher.Domain.Integrity;

public sealed record FileIntegrity
{
    public const int Sha256HexLength = 64;

    public long Length { get; }

    public string Sha256 { get; }

    public FileIntegrity(long length, string sha256)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "File length cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        if (!IsValidSha256(sha256))
        {
            throw new ArgumentException(
                $"SHA-256 must contain exactly {Sha256HexLength} lowercase hexadecimal characters.",
                nameof(sha256));
        }

        Length = length;
        Sha256 = sha256;
    }

    public bool Matches(FileIntegrity actual)
    {
        ArgumentNullException.ThrowIfNull(actual);

        return Length == actual.Length && string.Equals(Sha256, actual.Sha256, StringComparison.Ordinal);
    }

    public static FileIntegrity Create(ReadOnlySpan<byte> contents)
    {
        return new FileIntegrity(contents.Length, Convert.ToHexStringLower(SHA256.HashData(contents)));
    }

    public static FileIntegrity Create(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        return new FileIntegrity(stream.Length, Convert.ToHexStringLower(SHA256.HashData(stream)));
    }

    private static bool IsValidSha256(string sha256)
    {
        return sha256.Length == Sha256HexLength &&
               sha256.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}
