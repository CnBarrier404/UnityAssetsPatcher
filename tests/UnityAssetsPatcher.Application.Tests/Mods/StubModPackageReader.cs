using UnityAssetsPatcher.Application.Mods;
namespace UnityAssetsPatcher.Application.Tests.Mods;

internal sealed class StubModPackageReader : IModPackageReader
{
    public string? OpenedPath { get; private set; }
    public string? ManifestReadPath { get; private set; }

    private readonly Func<string, IModPackageSession> _open;
    private readonly Func<string, CancellationToken, Task<byte[]>> _readManifest;

    public StubModPackageReader(IModPackageSession session)
        : this(_ => session, (_, cancellationToken) => session.ReadManifestAsync(cancellationToken)) { }

    public StubModPackageReader(Func<string, IModPackageSession> open)
        : this(open, (_, _) => throw new NotSupportedException()) { }

    private StubModPackageReader(
        Func<string, IModPackageSession> open,
        Func<string, CancellationToken, Task<byte[]>> readManifest)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(readManifest);

        _open = open;
        _readManifest = readManifest;
    }

    public Task<byte[]> ReadManifestAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ManifestReadPath = packagePath;

        return _readManifest(packagePath, cancellationToken);
    }

    public IModPackageSession Open(string packagePath)
    {
        OpenedPath = packagePath;

        return _open(packagePath);
    }
}

internal sealed class StubModPackageSession : IModPackageSession
{
    public string? CopiedSource { get; private set; }
    public string? CopyDestinationPath { get; private set; }
    public bool IsDisposed { get; private set; }

    private readonly byte[] _manifestBytes;
    private readonly long _copiedBytes;

    public StubModPackageSession(byte[] manifestBytes, long copiedBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(manifestBytes);

        _manifestBytes = manifestBytes;
        _copiedBytes = copiedBytes;
    }

    public Task<byte[]> ReadManifestAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_manifestBytes);
    }

    public long CopyEntryToNewFile(
        string source,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CopiedSource = source;
        CopyDestinationPath = destinationPath;

        return _copiedBytes;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
