using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Application.Mods;

internal static partial class ModPackageLog
{
    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Debug,
        Message = "Decompressed package manifest {ManifestEntry} from {PackagePath}: " +
                  "{ByteCount} bytes in {ElapsedMilliseconds} ms")]
    public static partial void ManifestDecompressed(
        ILogger logger,
        string manifestEntry,
        string packagePath,
        long byteCount,
        double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Debug,
        Message = "Extracted package entry {EntryPath} from {PackagePath} to {DestinationPath}: " +
                  "{ByteCount} bytes in {ElapsedMilliseconds} ms")]
    public static partial void EntryExtracted(
        ILogger logger,
        string entryPath,
        string packagePath,
        string destinationPath,
        long byteCount,
        double elapsedMilliseconds);
}
