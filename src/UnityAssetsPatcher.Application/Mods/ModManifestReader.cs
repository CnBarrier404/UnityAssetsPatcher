using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public sealed class ModManifestReader
{
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly IModPackageReader _modPackageReader;

    public ModManifestReader(IFileSystemOperations fileSystemOperations, IModPackageReader modPackageReader)
    {
        ArgumentNullException.ThrowIfNull(fileSystemOperations);
        ArgumentNullException.ThrowIfNull(modPackageReader);

        _fileSystemOperations = fileSystemOperations;
        _modPackageReader = modPackageReader;
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

    private async Task<byte[]> ReadPackageManifestAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        return await _modPackageReader.ReadManifestAsync(packagePath, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadFileAsync(string sourcePath, CancellationToken cancellationToken)
    {
        await using Stream input = _fileSystemOperations.OpenRead(sourcePath);
        using MemoryStream output = new();

        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

        return output.ToArray();
    }

    private static bool IsPackagePath(string sourcePath)
    {
        return Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }
}
