using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Domain.Assets;

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
    public int ReadFieldCount { get; private set; }
    public IReadOnlyList<AssetReplacement> ReplacementPlan { get; private set; } = [];
    public bool? ReplacementSourcesExistedAtWrite { get; private set; }
    public IReadOnlyList<AssetCopy> CopyPlan { get; private set; } = [];
    public Exception? ReadFailure { get; init; }

    public IAssetsAccessScope CreateScope()
    {
        ScopeCreateCount++;

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
        ReadFieldCount++;
        _readPaths.Add(Path.GetFullPath(assetsFilePath));

        if (ReadFailure is not null)
        {
            throw ReadFailure;
        }

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
        ReplacementSourcesExistedAtWrite = plan.All(replacement => File.Exists(replacement.SourceAssetsFilePath));
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

    private sealed class StubAssetsAccessScope : IAssetsAccessScope
    {
        public IAssetsFileReader Reader
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_reader is null)
                {
                    _reader = new StubAssetsFileReader(_service);
                    _service.ReaderCreateCount++;
                }

                return _reader;
            }
        }

        public IAssetsFileWriter Writer
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                CloseReadSessionsCore();

                if (_writer is null)
                {
                    _writer = new StubAssetsFileWriter(_service);
                    _service.WriterCreateCount++;
                }

                return _writer;
            }
        }

        private readonly StubAssetsFileService _service;
        private StubAssetsFileReader? _reader;
        private StubAssetsFileWriter? _writer;
        private bool _disposed;

        public StubAssetsAccessScope(StubAssetsFileService service)
        {
            _service = service;
        }

        private void CloseReadSessionsCore()
        {
            _service.CloseReadSessionsCount++;
            _service.ReadFilesExistedAtClose = _service._readPaths.All(File.Exists);
            StubAssetsFileReader? reader = _reader;
            _reader = null;
            reader?.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StubAssetsFileReader? reader = _reader;
            StubAssetsFileWriter? writer = _writer;
            _reader = null;
            _writer = null;
            reader?.Dispose();
            writer?.Dispose();
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

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _service.ReadFilesExistedAtClose = _service._readPaths.All(File.Exists);
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
