using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Infrastructure.Mods;

internal static class ModPackageManifest
{
    private const int CopyBufferSize = 81920;
    private const long MaxManifestSize = 10L * 1024L * 1024L;

    public static ZipArchiveEntry FindEntry(
        ZipArchive archive,
        string packagePath,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry? manifestEntry = null;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedPath = entry.FullName.Replace('\\', '/');

            if (!GetFileName(normalizedPath).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ModPackageValidator.TryNormalizePath(entry.FullName, isDirectory: false, out _))
            {
                throw new InvalidDataException(
                    $"The package entry path is unsafe: {entry.FullName}. Package: {packagePath}");
            }

            if (manifestEntry is not null)
            {
                throw new InvalidDataException(
                    $"The package contains multiple manifest.json files. Package: {packagePath}");
            }

            manifestEntry = entry;
        }

        return manifestEntry ?? throw new InvalidDataException(
            $"The package does not contain a manifest.json file. Package: {packagePath}");
    }

    public static async Task<byte[]> ReadAsync(
        ZipArchiveEntry manifestEntry,
        string packagePath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (manifestEntry.Length > MaxManifestSize)
        {
            throw new InvalidDataException(
                $"The package manifest exceeds the {MaxManifestSize}-byte limit: " +
                $"{manifestEntry.FullName} ({manifestEntry.Length} bytes observed). Package: {packagePath}");
        }

        await using Stream input = await manifestEntry.OpenAsync(cancellationToken);
        using MemoryStream output = new((int)manifestEntry.Length);
        byte[] buffer = new byte[CopyBufferSize];
        long totalBytes = 0;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                totalBytes += bytesRead;

                if (totalBytes > MaxManifestSize)
                {
                    throw new InvalidDataException(
                        $"The package manifest exceeds the {MaxManifestSize}-byte limit: " +
                        $"{manifestEntry.FullName} ({totalBytes} bytes observed). Package: {packagePath}");
                }

                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            stopwatch.Stop();
        }

        ModPackageLog.ManifestDecompressed(
            logger,
            manifestEntry.FullName,
            packagePath,
            totalBytes,
            stopwatch.Elapsed.TotalMilliseconds);

        return output.ToArray();
    }

    private static string GetFileName(string normalizedPath)
    {
        int separatorIndex = normalizedPath.LastIndexOf('/');

        return separatorIndex < 0 ? normalizedPath : normalizedPath[(separatorIndex + 1)..];
    }
}
