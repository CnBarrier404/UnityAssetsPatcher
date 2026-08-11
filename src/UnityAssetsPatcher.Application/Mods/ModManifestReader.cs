using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public sealed class ModManifestReader
{
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly ModPackageReader _modPackageReader;

    public ModManifestReader(IFileSystemOperations fileSystemOperations, ModPackageReader modPackageReader)
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

        string fullSourcePath;

        try
        {
            fullSourcePath = Path.GetFullPath(sourcePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Failure(FileErrorCodes.InvalidPath, sourcePath);
        }

        try
        {
            ManifestSource source = await ReadSourceAsync(fullSourcePath, cancellationToken).ConfigureAwait(false);

            if (!source.IsPackage)
            {
                return ModManifestParser.Parse(source.ManifestBytes!);
            }

            OperationResult<byte[]> manifestResult = await _modPackageReader
                .ReadManifestAsync(fullSourcePath, cancellationToken)
                .ConfigureAwait(false);

            return manifestResult switch
            {
                OperationSucceeded<byte[]> succeeded => ModManifestParser.Parse(succeeded.Value),
                OperationFailed<byte[]> failed => new OperationFailed<ModManifest>(failed.Error),
                _ => throw new InvalidOperationException("The package reader returned an unknown result."),
            };
        }
        catch (FileNotFoundException)
        {
            return Failure(FileErrorCodes.NotFound, fullSourcePath);
        }
        catch (DirectoryNotFoundException)
        {
            return Failure(FileErrorCodes.NotFound, fullSourcePath);
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(FileErrorCodes.AccessDenied, fullSourcePath);
        }
        catch (IOException)
        {
            return Failure(FileErrorCodes.ReadFailed, fullSourcePath);
        }
    }

    private async Task<ManifestSource> ReadSourceAsync(string sourcePath, CancellationToken cancellationToken)
    {
        await using Stream input = _fileSystemOperations.OpenRead(sourcePath);
        byte[] prefix = new byte[4];
        int prefixLength = 0;

        while (prefixLength < prefix.Length)
        {
            int bytesRead = await input.ReadAsync(prefix.AsMemory(prefixLength), cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            prefixLength += bytesRead;
        }

        if (IsZipSignature(prefix.AsSpan(0, prefixLength)))
        {
            return new ManifestSource(true, null);
        }

        using MemoryStream output = new();

        await output.WriteAsync(prefix.AsMemory(0, prefixLength), cancellationToken).ConfigureAwait(false);

        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

        return new ManifestSource(false, output.ToArray());
    }

    private static bool IsZipSignature(ReadOnlySpan<byte> prefix)
    {
        return prefix is [(byte)'P', (byte)'K', 0x03, 0x04] or
            [(byte)'P', (byte)'K', 0x05, 0x06] or
            [(byte)'P', (byte)'K', 0x07, 0x08];
    }

    private sealed record ManifestSource(bool IsPackage, byte[]? ManifestBytes);

    private static OperationFailed<ModManifest> Failure(OperationErrorCode code, string path)
    {
        return new OperationFailed<ModManifest>(new OperationError(
            code,
            new Dictionary<string, object?>
            {
                ["path"] = path,
            }));
    }
}
