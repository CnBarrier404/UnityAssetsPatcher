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
        OperationResult<byte[]> manifestBytesResult = IsPackagePath(fullSourcePath)
            ? await ReadPackageManifestAsync(fullSourcePath, cancellationToken).ConfigureAwait(false)
            : new OperationSucceeded<byte[]>(
                await ReadFileAsync(fullSourcePath, cancellationToken).ConfigureAwait(false));

        if (manifestBytesResult is OperationFailed<byte[]> failure)
        {
            return new OperationFailed<ModManifest>(failure.Error);
        }

        byte[] manifestBytes = ((OperationSucceeded<byte[]>)manifestBytesResult).Value;

        return ModManifestParser.Parse(manifestBytes);
    }

    private async Task<OperationResult<byte[]>> ReadPackageManifestAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        OperationResult<IPackageSession> sessionResult = _packageReader.Open(packagePath);

        if (sessionResult is OperationFailed<IPackageSession> failure)
        {
            return new OperationFailed<byte[]>(failure.Error);
        }

        using IPackageSession session = ((OperationSucceeded<IPackageSession>)sessionResult).Value;

        return await session.ReadManifestAsync(cancellationToken).ConfigureAwait(false);
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
