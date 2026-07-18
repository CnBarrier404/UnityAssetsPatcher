namespace UnityAssetsPatcher.Application.Assets;

public sealed class ScopedAssetsAccess : IAssetsAccessScope
{
    public IAssetsFileReader Reader => _scope.Reader;
    public IAssetsFileWriter Writer => _scope.Writer;

    private readonly IAssetsAccessScope _scope;

    public ScopedAssetsAccess(IAssetsAccessScopeFactory scopeFactory)
    {
        _scope = scopeFactory.CreateScope();
    }

    public void CloseReadSessions()
    {
        _scope.CloseReadSessions();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}

public sealed class ScopedAssetsFileReader : IAssetsFileReader
{
    private readonly IAssetsAccessScope _scope;

    public ScopedAssetsFileReader(IAssetsAccessScope scope)
    {
        _scope = scope;
    }

    public IReadOnlyList<AssetsInfo> ReadAssetsInfo(string assetsFilePath)
    {
        return _scope.Reader.ReadAssetsInfo(assetsFilePath);
    }

    public AssetsFieldInfo ReadAssetsFieldInfo(string assetsFilePath, long pathId)
    {
        return _scope.Reader.ReadAssetsFieldInfo(assetsFilePath, pathId);
    }

    public void CloseReadSessions()
    {
        _scope.CloseReadSessions();
    }
}

public sealed class ScopedAssetsFileWriter : IAssetsFileWriter
{
    private readonly IAssetsAccessScope _scope;

    public ScopedAssetsFileWriter(IAssetsAccessScope scope)
    {
        _scope = scope;
    }

    public void WritePatch(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
    {
        _scope.Writer.WritePatch(inputPath, outputPath, plan);
    }

    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
    {
        _scope.Writer.WriteReplacements(inputPath, outputPath, plan);
    }
}
