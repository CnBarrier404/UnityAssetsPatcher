using UnityAssetsPatcher.Application.Mods;

namespace UnityAssetsPatcher.Application.Tests.Mods;

internal sealed class StubPackageReader : IPackageReader
{
    public string? OpenedPath { get; private set; }

    private readonly Func<string, string, CancellationToken, PackageContent> _read;
    private readonly Func<string, CancellationToken, Task<byte[]>> _readManifestAsync;

    public StubPackageReader(PackageContent content)
        : this((_, _, _) => content, (_, _) => Task.FromResult(content.Manifest)) { }

    public StubPackageReader(byte[] manifest)
        : this(
            (_, _, _) => new PackageContent(manifest, new Dictionary<string, string>()),
            (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return Task.FromResult(manifest);
            }) { }

    public StubPackageReader(Func<string, string, CancellationToken, PackageContent> read)
        : this(read, (_, _) => throw new NotSupportedException()) { }

    public StubPackageReader(Func<string, PackageContent> read)
        : this((path, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return read(path);
        }) { }

    public StubPackageReader(
        Func<string, string, CancellationToken, PackageContent> read,
        Func<string, CancellationToken, Task<byte[]>> readManifestAsync)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(readManifestAsync);

        _read = read;
        _readManifestAsync = readManifestAsync;
    }

    public PackageContent Read(
        string packagePath,
        string extractionDirectory,
        CancellationToken cancellationToken = default)
    {
        OpenedPath = packagePath;

        return _read(packagePath, extractionDirectory, cancellationToken);
    }

    public Task<byte[]> ReadManifestAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        OpenedPath = packagePath;

        return _readManifestAsync(packagePath, cancellationToken);
    }
}
