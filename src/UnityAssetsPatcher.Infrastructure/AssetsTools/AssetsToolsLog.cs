using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

internal static partial class AssetsToolsLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "Opening assets file {AssetsFilePath}")]
    public static partial void OpeningAssetsFile(ILogger logger, string assetsFilePath);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "Opened assets file {AssetsFilePath}")]
    public static partial void AssetsFileOpened(ILogger logger, string assetsFilePath);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "Read {AssetCount} assets from {AssetsFilePath}")]
    public static partial void AssetsRead(ILogger logger, int assetCount, string assetsFilePath);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Debug,
        Message = "Reading asset {PathId} from {AssetsFilePath}")]
    public static partial void ReadingAssetField(ILogger logger, long pathId, string assetsFilePath);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Debug,
        Message = "Writing {MutationCount} mutations from {AssetsFilePath} to {OutputPath}")]
    public static partial void WritingAssetsFile(
        ILogger logger,
        int mutationCount,
        string assetsFilePath,
        string outputPath);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Debug,
        Message = "Wrote {MutationCount} mutations to {OutputPath}")]
    public static partial void AssetsFileWritten(ILogger logger, int mutationCount, string outputPath);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Debug,
        Message = "Closed assets file {AssetsFilePath}")]
    public static partial void AssetsFileClosed(ILogger logger, string assetsFilePath);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Debug,
        Message = "Loading AssetsTools class package")]
    public static partial void LoadingClassPackage(ILogger logger);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Debug,
        Message = "Loaded AssetsTools class package")]
    public static partial void ClassPackageLoaded(ILogger logger);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Debug,
        Message = "Creating AssetsTools class database for Unity {UnityVersion}")]
    public static partial void CreatingClassDatabase(ILogger logger, string unityVersion);

    [LoggerMessage(
        EventId = 1013,
        Level = LogLevel.Debug,
        Message = "Created AssetsTools class database for Unity {UnityVersion}")]
    public static partial void ClassDatabaseCreated(ILogger logger, string unityVersion);
}
