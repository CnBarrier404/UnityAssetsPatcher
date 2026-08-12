using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Operations;

namespace UnityAssetsPatcher.Application.Mods;

public sealed class ModPackage : IDisposable
{
    public IReadOnlyDictionary<string, string> PatchSourcePaths { get; }
    public IReadOnlyList<string> AppliedOptionalGroups { get; }
    public ModManifest SourceManifest { get; }
    public ModManifest EffectiveManifest { get; }

    private readonly ModPackageSession _modPackageSession;
    private readonly IFileSystemOperations _fileSystemOperations;
    private readonly string? _temporaryDirectory;

    internal ModPackage(
        ModManifest sourceManifest,
        ModManifest effectiveManifest,
        IReadOnlyList<string> appliedOptionalGroups,
        IReadOnlyDictionary<string, string> patchSourcePaths,
        ModPackageSession modPackageSession,
        IFileSystemOperations fileSystemOperations,
        string? temporaryDirectory)
    {
        SourceManifest = sourceManifest;
        EffectiveManifest = effectiveManifest;
        AppliedOptionalGroups = appliedOptionalGroups;
        PatchSourcePaths = patchSourcePaths;
        _modPackageSession = modPackageSession;
        _fileSystemOperations = fileSystemOperations;
        _temporaryDirectory = temporaryDirectory;
    }

    public Task<OperationResult<long>> CopyPayloadFileAsync(
        string source,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        return _modPackageSession.CopyEntryToNewFileAsync(source, destinationPath, cancellationToken);
    }

    public void Dispose()
    {
        try
        {
            _modPackageSession.Dispose();
        }
        finally
        {
            if (_temporaryDirectory is not null && Directory.Exists(_temporaryDirectory))
            {
                _fileSystemOperations.DeleteDirectoryTree(_temporaryDirectory);
            }
        }
    }
}
