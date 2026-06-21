using System.IO.Compression;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Modules;
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
    private readonly PatchPlannerFactory _patchPlannerFactory;
    private readonly PatchAssetApplierFactory _patchAssetApplierFactory;
    private readonly ModManifestReader _manifestReader;
    private readonly GameDirectoryResolver _gameDirectoryResolver;
    private readonly Func<string, ZipArchive> _openPackageArchive;
    private readonly ManifestPatchOperationValidator _patchOperationValidator;
    private readonly TargetAssetResolver _targetAssetResolver;
    private readonly ModInstallationStoreFactory _recordStoreFactory;

    public InstallModWorkflowFactory(
        PatchPlannerFactory patchPlannerFactory,
        PatchAssetApplierFactory patchAssetApplierFactory,
        ModManifestReader manifestReader,
        GameDirectoryResolver gameDirectoryResolver,
        Func<string, ZipArchive> openPackageArchive,
        ManifestPatchOperationValidator patchOperationValidator,
        TargetAssetResolver targetAssetResolver,
        ModInstallationStoreFactory recordStoreFactory)
    {
        _patchPlannerFactory = patchPlannerFactory;
        _patchAssetApplierFactory = patchAssetApplierFactory;
        _manifestReader = manifestReader;
        _gameDirectoryResolver = gameDirectoryResolver;
        _openPackageArchive = openPackageArchive;
        _patchOperationValidator = patchOperationValidator;
        _targetAssetResolver = targetAssetResolver;
        _recordStoreFactory = recordStoreFactory;
    }

    public InstallModWorkflow Create(IAssetsAccessScope assets)
    {
        return new InstallModWorkflow(
            _patchPlannerFactory.Create(assets.Reader),
            _patchAssetApplierFactory.Create(assets.Writer),
            assets,
            _manifestReader,
            _gameDirectoryResolver,
            _openPackageArchive,
            _patchOperationValidator,
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

public sealed class PatchPlannerFactory
{
    private readonly PatchPlanBuilderFactory _patchPlanBuilderFactory;

    public PatchPlannerFactory(PatchPlanBuilderFactory patchPlanBuilderFactory)
    {
        _patchPlanBuilderFactory = patchPlanBuilderFactory;
    }

    public PatchPlanner Create(IAssetsFileReader assetsReader)
    {
        return new PatchPlanner(_patchPlanBuilderFactory.Create(assetsReader));
    }
}

public sealed class PatchAssetApplierFactory
{
    private readonly PatchOutputWriterFactory _patchOutputWriterFactory;

    public PatchAssetApplierFactory(PatchOutputWriterFactory patchOutputWriterFactory)
    {
        _patchOutputWriterFactory = patchOutputWriterFactory;
    }

    public PatchAssetApplier Create(IAssetsFileWriter assetsWriter)
    {
        return new PatchAssetApplier(_patchOutputWriterFactory.Create(assetsWriter));
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
