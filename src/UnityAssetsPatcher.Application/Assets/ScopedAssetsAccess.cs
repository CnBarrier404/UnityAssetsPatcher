using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.Application.Assets;

public sealed class ScopedAssetsAccess : IAssetsAccessScope
{
    public IAssetsFileReader Reader => GetScope().Reader;
    public IAssetsFileWriter Writer => GetScope().Writer;

    private readonly Lazy<IAssetsAccessScope> _scope;
    private bool _disposed;

    public ScopedAssetsAccess(IAssetsAccessScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scope = new Lazy<IAssetsAccessScope>(scopeFactory.CreateScope);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_scope.IsValueCreated)
        {
            _scope.Value.Dispose();
        }
    }

    private IAssetsAccessScope GetScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _scope.Value;
    }
}

public sealed class ScopedAssetsFileReader : IAssetsFileReader
{
    private readonly IAssetsAccessScope _scope;

    public ScopedAssetsFileReader(IAssetsAccessScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scope = scope;
    }

    public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
    {
        return _scope.Reader.ReadAssets(assetsFilePath);
    }

    public AssetField ReadField(string assetsFilePath, long pathId)
    {
        return _scope.Reader.ReadField(assetsFilePath, pathId);
    }
}

public sealed class ScopedAssetsFileWriter : IAssetsFileWriter
{
    private readonly IAssetsAccessScope _scope;

    public ScopedAssetsFileWriter(IAssetsAccessScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scope = scope;
    }

    public void WriteFieldPatches(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
    {
        _scope.Writer.WriteFieldPatches(inputPath, outputPath, plan);
    }

    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
    {
        _scope.Writer.WriteReplacements(inputPath, outputPath, plan);
    }

    public void WriteFieldPatchesAndCopies(
        string inputPath,
        string outputPath,
        IReadOnlyList<AssetFieldPatch> fieldPatches,
        IReadOnlyList<AssetCopy> copies)
    {
        _scope.Writer.WriteFieldPatchesAndCopies(inputPath, outputPath, fieldPatches, copies);
    }
}
