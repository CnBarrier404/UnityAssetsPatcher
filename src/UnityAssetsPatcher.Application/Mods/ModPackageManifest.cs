using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

internal static class ModPackageManifest
{
    private const int CopyBufferSize = 81920;
    private const long MaxManifestSize = 10L * 1024L * 1024L;

    public static OperationResult<IModPackageEntry> FindEntry(
        IModPackageSession package,
        string packagePath,
        CancellationToken cancellationToken)
    {
        IModPackageEntry? manifestEntry = null;

        foreach (IModPackageEntry entry in package.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedPath = entry.FullName.Replace('\\', '/');

            if (!GetFileName(normalizedPath).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ModPackageValidator.TryNormalizePath(entry.FullName, isDirectory: false, out _))
            {
                return Failure<IModPackageEntry>(
                    ModPackageErrorCodes.UnsafeEntryPath,
                    packagePath,
                    ("entry_path", entry.FullName));
            }

            if (manifestEntry is not null)
            {
                return Failure<IModPackageEntry>(ModPackageErrorCodes.MultipleManifests, packagePath);
            }

            manifestEntry = entry;
        }

        return manifestEntry is null
            ? Failure<IModPackageEntry>(ModPackageErrorCodes.MissingManifest, packagePath)
            : new OperationSucceeded<IModPackageEntry>(manifestEntry);
    }

    public static async Task<OperationResult<byte[]>> ReadAsync(
        IModPackageEntry manifestEntry,
        string packagePath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (manifestEntry.Length > MaxManifestSize)
        {
            return Failure<byte[]>(
                ModPackageErrorCodes.ManifestTooLarge,
                packagePath,
                ("entry_path", manifestEntry.FullName),
                ("maximum_bytes", MaxManifestSize),
                ("observed_bytes", manifestEntry.Length));
        }

        await using Stream input = await manifestEntry
            .OpenReadAsync(cancellationToken)
            .ConfigureAwait(false);
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
                    return Failure<byte[]>(
                        ModPackageErrorCodes.ManifestTooLarge,
                        packagePath,
                        ("entry_path", manifestEntry.FullName),
                        ("maximum_bytes", MaxManifestSize),
                        ("observed_bytes", totalBytes));
                }

                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            stopwatch.Stop();
        }

        if (totalBytes != manifestEntry.Length)
        {
            return Failure<byte[]>(
                ModPackageErrorCodes.EntrySizeMismatch,
                packagePath,
                ("entry_path", manifestEntry.FullName),
                ("declared_bytes", manifestEntry.Length),
                ("observed_bytes", totalBytes));
        }

        ModPackageLog.ManifestDecompressed(
            logger,
            manifestEntry.FullName,
            packagePath,
            totalBytes,
            stopwatch.Elapsed.TotalMilliseconds);

        return new OperationSucceeded<byte[]>(output.ToArray());
    }

    private static OperationFailed<T> Failure<T>(
        OperationErrorCode code,
        string packagePath,
        params (string Key, object? Value)[] additionalParameters)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["package_path"] = packagePath,
        };

        foreach ((string key, object? value) in additionalParameters)
        {
            parameters.Add(key, value);
        }

        return new OperationFailed<T>(new OperationError(code, parameters));
    }

    private static string GetFileName(string normalizedPath)
    {
        int separatorIndex = normalizedPath.LastIndexOf('/');

        return separatorIndex < 0 ? normalizedPath : normalizedPath[(separatorIndex + 1)..];
    }
}
