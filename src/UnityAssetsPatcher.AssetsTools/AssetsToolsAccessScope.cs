using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Abstractions.IO;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.AssetsTools;

public sealed class AssetsToolsAccessScope : IAssetsAccessScope
{
    public IAssetsFileReader Reader { get; }
    public IAssetsFileWriter Writer { get; }

    private readonly Lock _gate = new();
    private readonly AssetsFileReader _reader;
    private bool _writing;
    private bool _disposed;

    public AssetsToolsAccessScope(
        ClassPackageCache classPackageCache,
        IFileOperations fileOperations,
        IDirectoryOperations directoryOperations)
    {
        ArgumentNullException.ThrowIfNull(classPackageCache);
        ArgumentNullException.ThrowIfNull(fileOperations);
        ArgumentNullException.ThrowIfNull(directoryOperations);

        _reader = new AssetsFileReader(classPackageCache);
        Reader = new SynchronizedReader(this);
        Writer = new SynchronizedWriter(this,
            new AssetsFileWriter(classPackageCache, fileOperations, directoryOperations));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _reader.Dispose();
        }
    }

    private T Read<T>(Func<AssetsFileReader, T> operation)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_writing)
            {
                throw new InvalidOperationException(
                    "Assets files cannot be read while a write is in progress in the same scope.");
            }

            return operation.Invoke(_reader);
        }
    }

    private void Write(Action operation)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _reader.CloseSessions();
            _writing = true;

            try
            {
                operation.Invoke();
            }
            finally
            {
                _writing = false;
            }
        }
    }

    private sealed class SynchronizedReader : IAssetsFileReader
    {
        private readonly AssetsToolsAccessScope _scope;

        public SynchronizedReader(AssetsToolsAccessScope scope)
        {
            _scope = scope;
        }

        public IReadOnlyList<AssetInfo> ReadAssets(string assetsFilePath)
        {
            return _scope.Read(reader => reader.ReadAssets(assetsFilePath));
        }

        public AssetField ReadField(string assetsFilePath, long pathId)
        {
            return _scope.Read(reader => reader.ReadField(assetsFilePath, pathId));
        }
    }

    private sealed class SynchronizedWriter : IAssetsFileWriter
    {
        private readonly AssetsToolsAccessScope _scope;
        private readonly AssetsFileWriter _writer;

        public SynchronizedWriter(AssetsToolsAccessScope scope, AssetsFileWriter writer)
        {
            _scope = scope;
            _writer = writer;
        }

        public void WriteFieldPatches(string inputPath, string outputPath, IReadOnlyList<AssetFieldPatch> plan)
        {
            _scope.Write(() => _writer.WriteFieldPatches(inputPath, outputPath, plan));
        }

        public void WriteReplacements(string inputPath, string outputPath, IReadOnlyList<AssetReplacement> plan)
        {
            _scope.Write(() => _writer.WriteReplacements(inputPath, outputPath, plan));
        }

        public void WriteFieldPatchesAndCopies(
            string inputPath,
            string outputPath,
            IReadOnlyList<AssetFieldPatch> fieldPatches,
            IReadOnlyList<AssetCopy> copies)
        {
            _scope.Write(() => _writer.WriteFieldPatchesAndCopies(inputPath, outputPath, fieldPatches, copies));
        }
    }
}
