using System.Collections.Concurrent;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Infrastructure.AssetsTools;

internal sealed class ClassPackageCache
{
    private readonly ConcurrentDictionary<string, Lazy<byte[]>> _classDatabases = new(StringComparer.Ordinal);
    private readonly Lazy<ClassPackageFile> _classPackage;
    private readonly ILogger<ClassPackageCache> _logger;
    private readonly Func<Stream> _openTpkStream;

    public ClassPackageCache(Func<Stream> openTpkStream, ILogger<ClassPackageCache> logger)
    {
        ArgumentNullException.ThrowIfNull(openTpkStream);
        ArgumentNullException.ThrowIfNull(logger);

        _openTpkStream = openTpkStream;
        _logger = logger;
        _classPackage = new Lazy<ClassPackageFile>(LoadClassPackage, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public void EnsureLoaded()
    {
        _ = _classPackage.Value;
    }

    public void LoadClassDatabase(AssetsManager manager, string unityVersion)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(unityVersion);

        var database = _classDatabases.GetOrAdd(unityVersion,
            version => new Lazy<byte[]>(
                () => CreateClassDatabase(version),
                LazyThreadSafetyMode.ExecutionAndPublication));

        byte[] databaseBytes = database.Value;

        using MemoryStream databaseStream = new(databaseBytes, false);
        manager.LoadClassDatabase(databaseStream);
    }

    private ClassPackageFile LoadClassPackage()
    {
        AssetsToolsLog.LoadingClassPackage(_logger);

        using Stream tpkStream = _openTpkStream();
        var classPackage = new ClassPackageFile();
        var reader = new AssetsFileReader(tpkStream);
        classPackage.Read(reader);

        AssetsToolsLog.ClassPackageLoaded(_logger);

        return classPackage;
    }

    private byte[] CreateClassDatabase(string unityVersion)
    {
        AssetsToolsLog.CreatingClassDatabase(_logger, unityVersion);

        ClassDatabaseFile classDatabase = _classPackage.Value.GetClassDatabase(unityVersion);
        using MemoryStream databaseStream = new();
        var writer = new AssetsFileWriter(databaseStream);
        classDatabase.Write(writer, ClassFileCompressionType.Uncompressed);

        AssetsToolsLog.ClassDatabaseCreated(_logger, unityVersion);

        return databaseStream.ToArray();
    }
}
