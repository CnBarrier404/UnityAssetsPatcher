using UnityAssetsPatcher.Application.Assets;

namespace UnityAssetsPatcher.Tests.Support;

public sealed class StubAssetsFileService : IAssetsAccessScopeFactory, IAssetsFileReader, IAssetsFileWriter
{
    private readonly IReadOnlyList<AssetInfo> _result;
    private readonly IReadOnlyDictionary<long, AssetField> _fieldTrees;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AssetInfo>> _resultsByPath;
    private readonly IReadOnlyDictionary<(string AssetsFilePath, long PathId), AssetField> _fieldTreesByPath;
    private readonly HashSet<string> _readPaths = new(StringComparer.OrdinalIgnoreCase);

    public StubAssetsFileService(IReadOnlyList<AssetInfo> result)
        : this(result, new Dictionary<long, AssetField>()) { }

    public StubAssetsFileService(
        IReadOnlyList<AssetInfo> result,
        IReadOnlyDictionary<long, AssetField> fieldTrees)
    {
        _result = result;
        _fieldTrees = fieldTrees;
        _resultsByPath = new Dictionary<string, IReadOnlyList<AssetInfo>>(StringComparer.OrdinalIgnoreCase);
        _fieldTreesByPath = new Dictionary<(string AssetsFilePath, long PathId), AssetField>();
    }

    public StubAssetsFileService(
        IReadOnlyDictionary<string, IReadOnlyList<AssetInfo>> resultsByPath,
        IReadOnlyDictionary<(string AssetsFilePath, long PathId), AssetField> fieldTreesByPath)
    {
        _result = [];
        _fieldTrees = new Dictionary<long, AssetField>();
        _resultsByPath = resultsByPath;
        _fieldTreesByPath = fieldTreesByPath;
    }

    public bool WasCalled { get; private set; }
    public string? InputPath { get; private set; }
    public string? OutputPath { get; private set; }
    public int CloseReadSessionsCount { get; private set; }
    public int? CloseReadSessionsCountAtWrite { get; private set; }
    public bool? ReadFilesExistedAtClose { get; private set; }
    public int ScopeCreateCount { get; private set; }
    public int ScopeDisposeCount { get; private set; }
    public int ReaderCreateCount { get; private set; }
    public int ReaderDisposeCount { get; private set; }
    public int WriterCreateCount { get; private set; }
    public int WriterDisposeCount { get; private set; }
    public IReadOnlyList<AssetReplacement> ReplacementPlan { get; private set; } = [];
    public IReadOnlyList<AssetCopy> CopyPlan { get; private set; } = [];

    public IAssetsAccessScope CreateScope()
    {
        ScopeCreateCount++;
        ReaderCreateCount++;
        WriterCreateCount++;

        return new StubAssetsAccessScope(this);
    }

    public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
    {
        _readPaths.Add(Path.GetFullPath(assetsFilePath));

        if (_resultsByPath.TryGetValue(assetsFilePath, out var result))
        {
            return result;
        }

        return _resultsByPath.TryGetValue(Path.GetFileName(assetsFilePath), out result)
            ? result
            : _result;
    }

    public AssetField ReadField(string assetsFilePath, long pathId)
    {
        _readPaths.Add(Path.GetFullPath(assetsFilePath));

        if (_fieldTreesByPath.TryGetValue((assetsFilePath, pathId), out AssetField? fieldTreeByPath) ||
            _fieldTreesByPath.TryGetValue((Path.GetFileName(assetsFilePath), pathId), out fieldTreeByPath))
        {
            return fieldTreeByPath;
        }

        return _fieldTrees.TryGetValue(pathId, out AssetField? fieldTree)
            ? fieldTree
            : throw new InvalidOperationException("Field tree was not configured.");
    }

    public void WriteFieldPatches(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
    {
        WasCalled = true;
        InputPath = inputPath;
        OutputPath = outputPath;
        CloseReadSessionsCountAtWrite = CloseReadSessionsCount;
        File.WriteAllText(outputPath, "patched");
    }

    public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
    {
        WasCalled = true;
        InputPath = inputPath;
        OutputPath = outputPath;
        CloseReadSessionsCountAtWrite = CloseReadSessionsCount;
        ReplacementPlan = plan;
        File.WriteAllText(outputPath, "patched");
    }

    public void WriteFieldPatchesAndCopies(
        string inputPath,
        string outputPath,
        IReadOnlyList<AssetFieldPatch> fieldPatches,
        IReadOnlyList<AssetCopy> copies)
    {
        WasCalled = true;
        InputPath = inputPath;
        OutputPath = outputPath;
        CloseReadSessionsCountAtWrite = CloseReadSessionsCount;
        CopyPlan = copies;
        File.WriteAllText(outputPath, "patched");
    }

    public void Dispose() { }

    public void CloseReadSessions()
    {
        ReadFilesExistedAtClose = _readPaths.All(File.Exists);
        CloseReadSessionsCount++;
    }

    private sealed class StubAssetsAccessScope : IAssetsAccessScope
    {
        public IAssetsFileReader Reader { get; }
        public IAssetsFileWriter Writer { get; }

        private readonly StubAssetsFileService _service;
        private bool _disposed;

        public StubAssetsAccessScope(StubAssetsFileService service)
        {
            _service = service;
            Reader = new StubAssetsFileReader(service);
            Writer = new StubAssetsFileWriter(service);
        }

        public void CloseReadSessions()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Reader.CloseReadSessions();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Reader.Dispose();
            Writer.Dispose();
            _service.ScopeDisposeCount++;
        }
    }

    private sealed class StubAssetsFileReader : IAssetsFileReader
    {
        private readonly StubAssetsFileService _service;
        private bool _disposed;

        public StubAssetsFileReader(StubAssetsFileService service)
        {
            _service = service;
        }

        public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _service.ReadAssets(assetsFilePath);
        }

        public AssetField ReadField(string assetsFilePath, long pathId)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _service.ReadField(assetsFilePath, pathId);
        }

        public void CloseReadSessions()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _service.CloseReadSessions();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _service.ReaderDisposeCount++;
        }
    }

    private sealed class StubAssetsFileWriter : IAssetsFileWriter
    {
        private readonly StubAssetsFileService _service;
        private bool _disposed;

        public StubAssetsFileWriter(StubAssetsFileService service)
        {
            _service = service;
        }

        public void WriteFieldPatches(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _service.WriteFieldPatches(inputPath, outputPath, plan);
        }

        public void WriteReplacements(
            string inputPath,
            string outputPath,
            IReadOnlyList<AssetReplacement> plan)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _service.WriteReplacements(inputPath, outputPath, plan);
        }

        public void WriteFieldPatchesAndCopies(
            string inputPath,
            string outputPath,
            IReadOnlyList<AssetFieldPatch> fieldPatches,
            IReadOnlyList<AssetCopy> copies)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _service.WriteFieldPatchesAndCopies(inputPath, outputPath, fieldPatches, copies);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _service.WriterDisposeCount++;
        }
    }
}
