using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Infrastructure.Packages;

internal static partial class ModPackageLog
{
    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Debug,
        Message = "Opening mod package {PackagePath}")]
    public static partial void OpeningPackage(ILogger logger, string packagePath);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Debug,
        Message = "Opened mod package {PackagePath} with {EntryCount} entries")]
    public static partial void PackageOpened(ILogger logger, string packagePath, int entryCount);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Debug,
        Message = "Opening entry {EntryPath} from mod package {PackagePath}")]
    public static partial void OpeningEntry(ILogger logger, string entryPath, string packagePath);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Debug,
        Message = "Opened entry {EntryPath} from mod package {PackagePath}")]
    public static partial void EntryOpened(ILogger logger, string entryPath, string packagePath);

    [LoggerMessage(
        EventId = 4090,
        Level = LogLevel.Debug,
        Message = "Failed to open mod package {PackagePath}")]
    public static partial void PackageOpenFailed(ILogger logger, string packagePath, Exception exception);

    [LoggerMessage(
        EventId = 4091,
        Level = LogLevel.Debug,
        Message = "Failed to open entry {EntryPath} from mod package {PackagePath}")]
    public static partial void EntryOpenFailed(
        ILogger logger,
        string entryPath,
        string packagePath,
        Exception exception);
}
