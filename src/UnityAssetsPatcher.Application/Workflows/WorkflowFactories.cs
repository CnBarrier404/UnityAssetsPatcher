using System.IO.Compression;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Application.Workflows;

public interface IInstallModWorkflowFactory
{
    InstallModWorkflow Create(IAssetsAccessScope assets);
}

public interface IUninstallModWorkflowFactory
{
    UninstallModWorkflow Create(string backupDirectory);
}

public sealed class InstallModWorkflowFactory : IInstallModWorkflowFactory
{
    private readonly PatchPlanBuilderFactory _patchPlanBuilderFactory;
    private readonly PatchOutputWriterFactory _patchOutputWriterFactory;
    private readonly ModManifestReader _manifestReader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly Func<string, ZipArchive> _openPackageArchive;
    private readonly TargetAssetResolver _targetAssetResolver;
    private readonly ModInstallationStoreFactory _recordStoreFactory;

    public InstallModWorkflowFactory(
        PatchPlanBuilderFactory patchPlanBuilderFactory,
        PatchOutputWriterFactory patchOutputWriterFactory,
        ModManifestReader manifestReader,
        GameDirectoryResolver gameDirectoryResolver,
        Func<string, ZipArchive> openPackageArchive,
        TargetAssetResolver targetAssetResolver,
        ModInstallationStoreFactory recordStoreFactory)
    {
        _patchPlanBuilderFactory = patchPlanBuilderFactory;
        _patchOutputWriterFactory = patchOutputWriterFactory;
        _manifestReader = manifestReader;
        _gameDirectoryResolver = gameDirectoryResolver;
        _openPackageArchive = openPackageArchive;
        _targetAssetResolver = targetAssetResolver;
        _recordStoreFactory = recordStoreFactory;
    }

    public InstallModWorkflow Create(IAssetsAccessScope assets)
    {
        return new InstallModWorkflow(
            _patchPlanBuilderFactory.Create(assets.Reader),
            _patchOutputWriterFactory.Create(assets.Writer),
            assets,
            _manifestReader,
            _gameDirectoryResolver,
            _openPackageArchive,
            _targetAssetResolver,
            _recordStoreFactory);
    }
}

public sealed class UninstallModWorkflowFactory : IUninstallModWorkflowFactory
{
    private readonly ModInstallationStoreFactory _recordStoreFactory;

    public UninstallModWorkflowFactory(ModInstallationStoreFactory recordStoreFactory)
    {
        _recordStoreFactory = recordStoreFactory;
    }

    public UninstallModWorkflow Create(string backupDirectory)
    {
        return new UninstallModWorkflow(_recordStoreFactory.Create(backupDirectory));
    }
}

public sealed class PatchPlanBuilderFactory
{
    private readonly AssetQueryServiceFactory _assetQueryServiceFactory;

    public PatchPlanBuilderFactory(AssetQueryServiceFactory assetQueryServiceFactory)
    {
        _assetQueryServiceFactory = assetQueryServiceFactory;
    }

    public PatchPlanBuilder Create(IAssetsFileReader assetsReader)
    {
        AssetQueryService assetQueryService = _assetQueryServiceFactory.Create(assetsReader);

        return new PatchPlanBuilder(
            new FieldPatchPlanBuilder(assetQueryService),
            new ReplacementPlanBuilder(assetQueryService));
    }
}

public sealed class AssetQueryServiceFactory
{
    public AssetQueryService Create(IAssetsFileReader assetsReader)
    {
        return new AssetQueryService(assetsReader);
    }
}

public sealed class PatchOutputWriterFactory
{
    public PatchOutputWriter Create(IAssetsFileWriter assetsWriter)
    {
        return new PatchOutputWriter(assetsWriter);
    }
}

public sealed class ModInstallationStoreFactory
{
    public ModInstallationStore Create(string backupDirectory)
    {
        return new ModInstallationStore(backupDirectory);
    }
}
