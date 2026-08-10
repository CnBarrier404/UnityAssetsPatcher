using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public sealed class ModManifestReader
{
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly IPackageReader _packageReader;

    public ModManifestReader(IFileSystemOperations fileSystemOperations, IPackageReader packageReader)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(packageReader);

        _fileSystemOperations = fileSystemOperations;
        _packageReader = packageReader;
    }

    public async Task<OperationResult<ModManifest>> ReadAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullSourcePath = Path.GetFullPath(sourcePath);
        byte[] manifestBytes = IsPackagePath(fullSourcePath)
            ? await ReadPackageManifestAsync(fullSourcePath, cancellationToken).ConfigureAwait(false)
            : await ReadFileAsync(fullSourcePath, cancellationToken).ConfigureAwait(false);

        return ModManifestParser.Parse(manifestBytes);
    }

    private Task<byte[]> ReadPackageManifestAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        return _packageReader.ReadManifestAsync(packagePath, cancellationToken);
    }

    private async Task<byte[]> ReadFileAsync(string sourcePath, CancellationToken cancellationToken)
    {
        using Stream input = _fileSystemOperations.OpenRead(sourcePath);
        using MemoryStream output = new();

        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

        return output.ToArray();
    }

    private static bool IsPackagePath(string sourcePath)
    {
        return Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }
}
