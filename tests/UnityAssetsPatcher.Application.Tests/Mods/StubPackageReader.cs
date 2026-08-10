using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Tests.Mods;

internal sealed class StubPackageReader : IPackageReader
{
    public string? OpenedPath { get; private set; }

    private readonly Func<string, OperationResult<IPackageSession>> _open;

    public StubPackageReader(IPackageSession session)
        : this(_ => new OperationSucceeded<IPackageSession>(session)) { }

    public StubPackageReader(Func<string, OperationResult<IPackageSession>> open)
    {
        ArgumentNullException.ThrowIfNull(open);

        _open = open;
    }

    public OperationResult<IPackageSession> Open(string packagePath)
    {
        OpenedPath = packagePath;

        return _open(packagePath);
    }
}

internal sealed class StubPackageSession : IPackageSession
{
    public string? CopiedSource { get; private set; }
    public string? CopyDestinationPath { get; private set; }
    public bool IsDisposed { get; private set; }

    private readonly byte[] _manifestBytes;
    private readonly OperationResult<long> _copyResult;

    public StubPackageSession(byte[] manifestBytes, OperationResult<long>? copyResult = null)
    {
        ArgumentNullException.ThrowIfNull(manifestBytes);

        _manifestBytes = manifestBytes;
        _copyResult = copyResult ?? new OperationSucceeded<long>(0);
    }

    public OperationResult<byte[]> ReadManifest()
    {
        return new OperationSucceeded<byte[]>(_manifestBytes);
    }

    public Task<OperationResult<byte[]>> ReadManifestAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<OperationResult<byte[]>>(new OperationSucceeded<byte[]>(_manifestBytes));
    }

    public OperationResult<long> CopyEntryToNewFile(
        string source,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CopiedSource = source;
        CopyDestinationPath = destinationPath;

        return _copyResult;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
