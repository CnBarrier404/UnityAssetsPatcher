using System.Collections.Concurrent;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class ClassPackageCache
{
    private readonly ConcurrentDictionary<string, Lazy<byte[]>> _classDatabases = new(StringComparer.Ordinal);
    private readonly Lazy<ClassPackageFile> _classPackage;
    private readonly Func<Stream> _openTpkStream;

    public ClassPackageCache(Func<Stream> openTpkStream)
    {
        ArgumentNullException.ThrowIfNull(openTpkStream);

        _openTpkStream = openTpkStream;
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

        using MemoryStream databaseStream = new(databaseBytes, writable: false);
        manager.LoadClassDatabase(databaseStream);
    }

    private ClassPackageFile LoadClassPackage()
    {
        using Stream tpkStream = _openTpkStream();
        var classPackage = new ClassPackageFile();
        var reader = new global::AssetsTools.NET.AssetsFileReader(tpkStream);
        classPackage.Read(reader);

        return classPackage;
    }

    private byte[] CreateClassDatabase(string unityVersion)
    {
        ClassDatabaseFile classDatabase = _classPackage.Value.GetClassDatabase(unityVersion);
        using MemoryStream databaseStream = new();
        var writer = new global::AssetsTools.NET.AssetsFileWriter(databaseStream);
        classDatabase.Write(writer, ClassFileCompressionType.Uncompressed);

        return databaseStream.ToArray();
    }
}
