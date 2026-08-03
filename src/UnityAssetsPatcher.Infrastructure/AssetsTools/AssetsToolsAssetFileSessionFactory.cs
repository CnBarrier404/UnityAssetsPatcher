using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

public sealed class AssetsToolsAssetFileSessionFactory : IAssetFileSessionFactory
{
    private readonly ClassPackageCache _classPackageCache;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ILogger<AssetsToolsAssetFileSessionFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public AssetsToolsAssetFileSessionFactory(
        Func<Stream> openClassPackage,
        IFileSystemOperations fileSystemOperations,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(openClassPackage);
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _classPackageCache = new ClassPackageCache(
            openClassPackage,
            loggerFactory.CreateLogger<ClassPackageCache>());
        _fileSystemOperations = fileSystemOperations;
        _logger = loggerFactory.CreateLogger<AssetsToolsAssetFileSessionFactory>();
        _loggerFactory = loggerFactory;
    }

    public IAssetFileSession Open(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        string fullPath = Path.GetFullPath(inputPath);

        AssetsToolsLog.OpeningAssetsFile(_logger, fullPath);

        return new AssetsToolsAssetFileSession(
            fullPath,
            _classPackageCache,
            _fileSystemOperations,
            _loggerFactory.CreateLogger<AssetsToolsAssetFileSession>());
    }
}
