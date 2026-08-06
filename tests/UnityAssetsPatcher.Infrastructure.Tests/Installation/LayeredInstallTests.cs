using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Assets;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Install;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Domain.Integrity;
using UnityAssetsPatcher.Infrastructure.Tests.Repository;
using Xunit;

namespace UnityAssetsPatcher.Infrastructure.Tests.Installation;

public sealed class LayeredInstallTests
{
    [Fact]
    public void Install_WhenRepositoryIsNew_CapturesBaseAndPersistsSingleLayer()
    {
        using LayeredInstallFixture fixture = new();
        string packagePath = fixture.CreatePackage("single", CreateFieldManifest("Text", "Single Layer"));

        InstallModResult result = fixture.Install(packagePath);

        Assert.Equal(1, result.BaseSnapshotCount);
        LayerRecordEntry layer = Assert.Single(fixture.Repository.Layers.ListLayers());
        Assert.Equal(result.InstallId, layer.Record.Id);
        Assert.Equal("Single Layer", fixture.ReadName(fixture.GameAssetsPath));
        Assert.True(File.Exists(fixture.Repository.Layers.ResolvePackagePath(result.InstallId)));
        Assert.True(File.Exists(fixture.BaseAssetsPath));
    }

    [Fact]
    public void Install_WhenTwoModsTargetSameAssetsFile_ComposesBothLayers()
    {
        using LayeredInstallFixture fixture = new();
        string firstPackage = fixture.CreatePackage("first", CreateFieldManifest("Text", "Layer One"));
        string secondPackage = fixture.CreatePackage("second", CreateFieldManifest("Layer One", "Layer Two"));

        InstallModResult first = fixture.Install(firstPackage);
        InstallModResult second = fixture.Install(secondPackage);

        Assert.Equal(1, first.BaseSnapshotCount);
        Assert.Equal(0, second.BaseSnapshotCount);
        Assert.Equal("Layer Two", fixture.ReadName(fixture.GameAssetsPath));
        LayerRecordEntry[] layers = [.. fixture.Repository.Layers.ListLayers()];
        Assert.Equal([2L, 1L], layers.Select(entry => entry.Record.InstallSequence).ToArray());
        Assert.True(File.Exists(fixture.Repository.Layers.ResolvePackagePath(first.InstallId)));
        Assert.True(File.Exists(fixture.Repository.Layers.ResolvePackagePath(second.InstallId)));
        Assert.Equal(
            File.ReadAllBytes(firstPackage),
            File.ReadAllBytes(fixture.Repository.Layers.ResolvePackagePath(first.InstallId)));
        Assert.Equal(
            File.ReadAllBytes(secondPackage),
            File.ReadAllBytes(fixture.Repository.Layers.ResolvePackagePath(second.InstallId)));
    }

    [Fact]
    public void Install_WhenRepositoryUsesVersionOne_ReturnsUnsupportedRepositoryVersion()
    {
        using LayeredInstallFixture fixture = new(repositoryVersion: 1);
        OperationResult<InstallModResult> result = fixture.InstallOperation(
            new InstallRequest("missing.zip", fixture.GameDirectory));

        OperationFailed<InstallModResult> failed = Assert.IsType<OperationFailed<InstallModResult>>(result);

        Assert.Equal(WorkflowErrorCodes.UnsupportedRepositoryVersion, failed.Error.Code);
        Assert.Contains("legacy format", failed.Error.Parameters["detail"]?.ToString());
    }

    [Fact]
    public void Install_WhenPreparedPreviewIsUsed_ReanalyzesAndStoresLayer()
    {
        using LayeredInstallFixture fixture = new();
        string packagePath = fixture.CreatePackage("prepared", CreateFieldManifest("Text", "Prepared"));
        InstallPreviewResult preview = fixture.Preview(new InstallRequest(packagePath, fixture.GameDirectory));
        InstallRequest installRequest = new(packagePath, fixture.GameDirectory)
        {
            PreparedInstall = preview.PreparedInstall,
        };

        InstallModResult result = Assert.IsType<OperationSucceeded<InstallModResult>>(
            fixture.InstallOperation(installRequest)).Value;

        Assert.Equal(1, result.BaseSnapshotCount);
        Assert.Equal("Prepared", fixture.ReadName(fixture.GameAssetsPath));
    }

    [Fact]
    public void Install_WhenPlanningFailsBeforeTransaction_HasNoSideEffects()
    {
        using LayeredInstallFixture fixture = new();
        string packagePath = fixture.CreatePackage("planning-failure", CreateFieldManifest("Missing", "Never Applied"));

        OperationResult<InstallModResult> result = fixture.InstallOperation(
            new InstallRequest(packagePath, fixture.GameDirectory));

        OperationFailed<InstallModResult> failed = Assert.IsType<OperationFailed<InstallModResult>>(result);

        Assert.Equal(WorkflowErrorCodes.PatchPlanningFailed, failed.Error.Code);
        Assert.Equal("Text", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Empty(fixture.Repository.Layers.ListLayers());
        Assert.False(Directory.Exists(Path.Combine(fixture.Repository.RepositoryDirectory, "games")));
        Assert.False(Directory.Exists(fixture.Repository.TransactionDirectory));
    }

    [Fact]
    public void Uninstall_WhenLatestLayerIsRemoved_ReplaysBaseAndRemovesLayer()
    {
        using LayeredInstallFixture fixture = new();
        string packagePath = fixture.CreatePackage("uninstall", CreateFieldManifest("Text", "Uninstalled"));
        InstallModResult installed = fixture.Install(packagePath);

        UninstallPreviewResult preview = fixture.PreviewUninstall(
            new UninstallPreviewRequest(installed.InstallId, fixture.GameDirectory));

        Assert.True(preview.CanUninstall);
        Assert.Single(preview.ChangedFiles);
        Assert.Equal(UninstallChangedFileAction.RestoreBase, preview.ChangedFiles[0].Action);

        UninstallModResult result = fixture.Uninstall(
            new UninstallModRequest(installed.InstallId, fixture.GameDirectory));

        Assert.Single(result.ChangedFiles);
        Assert.Equal("Text", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Empty(fixture.Repository.Layers.ListLayers());
    }

    [Fact]
    public void Uninstall_WhenMiddleLayerIsRemoved_ReplaysEarlierLayersAndKeepsLaterLayer()
    {
        using LayeredInstallFixture fixture = new();
        InstallModResult first = fixture.Install(
            fixture.CreatePackage("first", CreateFieldManifest("Text", "Layer One")));
        InstallModResult middle = fixture.Install(
            fixture.CreatePackage("middle", CreateFieldManifest("Layer One", "Layer Two")));
        InstallModResult later = fixture.Install(
            fixture.CreatePackage(
                "later",
                CreateFieldManifestForFile("other.assets", "Text", "Later Layer")));

        Assert.Equal("Layer Two", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Equal("Later Layer", fixture.ReadName(fixture.OtherAssetsPath));

        UninstallPreviewResult preview = fixture.PreviewUninstall(
            new UninstallPreviewRequest(middle.InstallId, fixture.GameDirectory));

        Assert.True(preview.CanUninstall);
        Assert.Empty(preview.DependencyFailures);
        Assert.Equal(UninstallChangedFileAction.Rebuild, Assert.Single(preview.ChangedFiles).Action);

        UninstallModResult result = fixture.Uninstall(
            new UninstallModRequest(middle.InstallId, fixture.GameDirectory));

        Assert.Single(result.ChangedFiles);
        Assert.Equal("Layer One", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Equal("Later Layer", fixture.ReadName(fixture.OtherAssetsPath));
        Assert.Equal(
            [later.InstallId, first.InstallId],
            fixture.Repository.Layers.ListLayers().Select(entry => entry.Record.Id).ToArray());
    }

    [Fact]
    public void Uninstall_WhenRemovingLayerBreaksRemainingPatch_ReportsDependencyAndRejectsApply()
    {
        using LayeredInstallFixture fixture = new();
        InstallModResult first = fixture.Install(
            fixture.CreatePackage("first", CreateFieldManifest("Text", "Layer One")));
        InstallModResult second = fixture.Install(
            fixture.CreatePackage("second", CreateValueMismatchManifest("Layer One", "Layer Two")));

        UninstallPreviewResult preview = fixture.PreviewUninstall(
            new UninstallPreviewRequest(first.InstallId, fixture.GameDirectory));

        Assert.False(preview.CanUninstall);
        UninstallDependencyFailureResult failure = Assert.Single(preview.DependencyFailures);
        Assert.Equal("Layered Install Test Mod", failure.ModName);
        Assert.Equal(second.InstallId, fixture.Repository.Layers.ListLayers()[0].Record.Id);
        Assert.Equal(PatchDiagnosticCode.ValueMismatch, failure.Diagnostic.Code);

        OperationResult<UninstallModResult> result = fixture.UninstallOperation(
            new UninstallModRequest(first.InstallId, fixture.GameDirectory));

        OperationFailed<UninstallModResult> failed = Assert.IsType<OperationFailed<UninstallModResult>>(result);
        Assert.Equal(WorkflowErrorCodes.FileIntegrityMismatch, failed.Error.Code);
        Assert.Equal("Layer Two", fixture.ReadName(fixture.GameAssetsPath));
    }

    [Fact]
    public void Uninstall_WhenPayloadLayersOverlap_FallsBackThenDeletesAbsentBase()
    {
        using LayeredInstallFixture fixture = new();
        InstallModResult first = fixture.Install(fixture.CreatePackage(
            "payload-first",
            CreatePayloadManifest("config.txt"),
            ("config.txt", "first"u8.ToArray())));
        InstallModResult second = fixture.Install(fixture.CreatePackage(
            "payload-second",
            CreatePayloadManifest("config.txt"),
            ("config.txt", "second"u8.ToArray())));

        Assert.Equal("second", File.ReadAllText(fixture.PayloadPath));

        UninstallModResult result = fixture.Uninstall(
            new UninstallModRequest(second.InstallId, fixture.GameDirectory));

        Assert.Equal(2, result.ChangedFiles.Count);

        Assert.Equal("first", File.ReadAllText(fixture.PayloadPath));

        _ = fixture.Uninstall(new UninstallModRequest(first.InstallId, fixture.GameDirectory));

        Assert.False(File.Exists(fixture.PayloadPath));
    }

    [Fact]
    public void Uninstall_WhenCurrentGameFileWasModified_RejectsPreviewAndApply()
    {
        using LayeredInstallFixture fixture = new();
        InstallModResult installed = fixture.Install(
            fixture.CreatePackage("modified", CreateFieldManifest("Text", "Modified")));
        File.AppendAllText(fixture.GameAssetsPath, "external");

        UninstallPreviewResult preview = fixture.PreviewUninstall(
            new UninstallPreviewRequest(installed.InstallId, fixture.GameDirectory));

        Assert.False(preview.CanUninstall);
        Assert.Equal(FileIntegrityStatus.Modified, Assert.Single(preview.ChangedFiles).Status);

        OperationResult<UninstallModResult> result = fixture.UninstallOperation(
            new UninstallModRequest(installed.InstallId, fixture.GameDirectory));

        OperationFailed<UninstallModResult> failed = Assert.IsType<OperationFailed<UninstallModResult>>(result);
        Assert.Equal(WorkflowErrorCodes.FileIntegrityMismatch, failed.Error.Code);
        Assert.Single(fixture.Repository.Layers.ListLayers());
    }

    [Fact]
    public void Uninstall_WhenLayerPackageIsCorrupted_RejectsPreviewWithActionableError()
    {
        using LayeredInstallFixture fixture = new();
        InstallModResult installed = fixture.Install(
            fixture.CreatePackage("corrupt-package", CreateFieldManifest("Text", "Corrupted")));
        string storedPackagePath = fixture.Repository.Layers.ResolvePackagePath(installed.InstallId);
        File.AppendAllText(storedPackagePath, "corrupted");

        OperationResult<UninstallPreviewResult> result = fixture.PreviewUninstallOperation(
            new UninstallPreviewRequest(installed.InstallId, fixture.GameDirectory));

        OperationFailed<UninstallPreviewResult> failed = Assert.IsType<OperationFailed<UninstallPreviewResult>>(result);

        Assert.Contains("integrity", failed.Error.Parameters["detail"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Corrupted", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Single(fixture.Repository.Layers.ListLayers());
    }

    [Fact]
    public void Install_WhenSameModIsInstalledTwice_RejectsSecondLayerWithoutMutation()
    {
        using LayeredInstallFixture fixture = new();
        string packagePath = fixture.CreatePackage("duplicate", CreateFieldManifest("Text", "Duplicate"));
        InstallModResult first = fixture.Install(packagePath);

        OperationResult<InstallModResult> result = fixture.InstallOperation(
            new InstallRequest(packagePath, fixture.GameDirectory));

        OperationFailed<InstallModResult> failed = Assert.IsType<OperationFailed<InstallModResult>>(result);

        Assert.Equal(WorkflowErrorCodes.PatchPlanningFailed, failed.Error.Code);
        Assert.Equal("Duplicate", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Single(fixture.Repository.Layers.ListLayers());
        Assert.Equal(first.InstallId, fixture.Repository.Layers.ListLayers()[0].Record.Id);
        Assert.False(Directory.Exists(fixture.Repository.TransactionDirectory));
    }

    [Fact]
    public void Recover_WhenInstallInterruptedBeforeLayerCommit_RollsBackFilesAndRemovesLayer()
    {
        using LayeredInstallFixture fixture = new();
        InstallModResult installed = fixture.Install(
            fixture.CreatePackage("interrupted-install", CreateFieldManifest("Text", "Interrupted")));
        string basePath = fixture.BaseAssetsPath;
        fixture.Repository.Layers.DeleteLayer(installed.InstallId);
        fixture.CreatePendingTransaction(
            installed.InstallId,
            RepositoryOperationKind.Install,
            basePath,
            fixture.GameAssetsPath,
            applyAfter: false);

        RepositoryRecoveryPreview preview = fixture.PreviewRecovery();
        RepositoryRecoveryReport report = fixture.Recover();

        Assert.Equal(RepositoryRecoveryPlanAction.RollBack, preview.Action);
        Assert.True(preview.CanRecover);
        Assert.Equal(RepositoryRecoveryStatus.Recovered, report.Status);
        Assert.Equal("Text", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Empty(fixture.Repository.Layers.ListLayers());
        Assert.False(Directory.Exists(fixture.Repository.TransactionDirectory));

        InstallModResult reinstalled = fixture.Install(
            fixture.CreatePackage("after-recovery", CreateFieldManifest("Text", "After Recovery")));
        _ = fixture.Uninstall(new UninstallModRequest(reinstalled.InstallId, fixture.GameDirectory));

        Assert.Equal("Text", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Empty(fixture.Repository.Layers.ListLayers());
    }

    [Fact]
    public void Recover_WhenInstallCommittedBeforeTransactionCleanup_CompletesCleanup()
    {
        using LayeredInstallFixture fixture = new();
        InstallModResult installed = fixture.Install(
            fixture.CreatePackage("committed-install", CreateFieldManifest("Text", "Committed")));
        fixture.CreatePendingTransaction(
            installed.InstallId,
            RepositoryOperationKind.Install,
            fixture.BaseAssetsPath,
            fixture.GameAssetsPath,
            applyAfter: false);

        RepositoryRecoveryPreview preview = fixture.PreviewRecovery();
        RepositoryRecoveryReport report = fixture.Recover();

        Assert.Equal(RepositoryRecoveryPlanAction.CompleteCleanup, preview.Action);
        Assert.True(preview.CanRecover);
        Assert.Equal(RepositoryRecoveryStatus.Recovered, report.Status);
        Assert.Equal("Committed", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Single(fixture.Repository.Layers.ListLayers());
        Assert.False(Directory.Exists(fixture.Repository.TransactionDirectory));
    }

    [Fact]
    public void Recover_WhenUninstallInterruptedBeforeLayerMove_RestoresFilesAndKeepsLayer()
    {
        using LayeredInstallFixture fixture = new();
        InstallModResult installed = fixture.Install(
            fixture.CreatePackage("interrupted-uninstall", CreateFieldManifest("Text", "Interrupted")));
        fixture.CreatePendingTransaction(
            installed.InstallId,
            RepositoryOperationKind.Uninstall,
            fixture.GameAssetsPath,
            fixture.BaseAssetsPath,
            applyAfter: true);

        RepositoryRecoveryPreview preview = fixture.PreviewRecovery();
        RepositoryRecoveryReport report = fixture.Recover();

        Assert.Equal(RepositoryRecoveryPlanAction.RollBack, preview.Action);
        Assert.True(preview.CanRecover);
        Assert.Equal(RepositoryRecoveryStatus.Recovered, report.Status);
        Assert.Equal("Interrupted", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Single(fixture.Repository.Layers.ListLayers());
        Assert.False(Directory.Exists(fixture.Repository.TransactionDirectory));
    }

    [Fact]
    public void Recover_WhenUninstallLayerWasMovedBeforeCleanup_CompletesCleanup()
    {
        using LayeredInstallFixture fixture = new();
        InstallModResult installed = fixture.Install(
            fixture.CreatePackage("committed-uninstall", CreateFieldManifest("Text", "Committed")));
        fixture.CreatePendingTransaction(
            installed.InstallId,
            RepositoryOperationKind.Uninstall,
            fixture.GameAssetsPath,
            fixture.BaseAssetsPath,
            applyAfter: true,
            moveLayerToRemoved: true);

        RepositoryRecoveryPreview preview = fixture.PreviewRecovery();
        RepositoryRecoveryReport report = fixture.Recover();

        Assert.Equal(RepositoryRecoveryPlanAction.CompleteCleanup, preview.Action);
        Assert.True(preview.CanRecover);
        Assert.Equal(RepositoryRecoveryStatus.Recovered, report.Status);
        Assert.Equal("Text", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Empty(fixture.Repository.Layers.ListLayers());
        Assert.False(Directory.Exists(fixture.Repository.TransactionDirectory));
    }

    [Fact]
    public void Recover_WhenUninstallLayerWasMovedBeforeFileReplacement_RestoresLayer()
    {
        using LayeredInstallFixture fixture = new();
        InstallModResult installed = fixture.Install(
            fixture.CreatePackage("moved-uninstall", CreateFieldManifest("Text", "Moved")));
        fixture.CreatePendingTransaction(
            installed.InstallId,
            RepositoryOperationKind.Uninstall,
            fixture.GameAssetsPath,
            fixture.BaseAssetsPath,
            applyAfter: false,
            moveLayerToRemoved: true);

        RepositoryRecoveryPreview preview = fixture.PreviewRecovery();
        RepositoryRecoveryReport report = fixture.Recover();

        Assert.Equal(RepositoryRecoveryPlanAction.RollBack, preview.Action);
        Assert.True(preview.CanRecover);
        Assert.Equal(RepositoryRecoveryStatus.Recovered, report.Status);
        Assert.Equal("Moved", fixture.ReadName(fixture.GameAssetsPath));
        Assert.Single(fixture.Repository.Layers.ListLayers());
        Assert.False(Directory.Exists(fixture.Repository.TransactionDirectory));
    }

    private const string SchemaUri = "https://uap.cnbarrier.com/schema-v1.json";

    private static byte[] CreateFieldManifest(string from, string to)
    {
        return CreateFieldManifestForFile("sharedassets0.assets", from, to);
    }

    private static byte[] CreateFieldManifestForFile(string fileName, string from, string to)
    {
        JsonObject root = new()
        {
            ["$schema"] = SchemaUri,
            ["name"] = "Layered Install Test Mod",
            ["author"] = "Layered Install Tests",
            ["version"] = "1.0.0",
            ["targets"] = new JsonArray
            {
                new JsonObject
                {
                    ["file"] = fileName,
                    ["patches"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "TextAsset",
                            ["match"] = new JsonObject { ["m_Name"] = from },
                            ["set"] = new JsonObject
                            {
                                ["m_Name"] = new JsonObject
                                {
                                    ["from"] = from,
                                    ["to"] = to,
                                },
                            },
                        },
                    },
                },
            },
        };

        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static byte[] CreateValueMismatchManifest(string from, string to)
    {
        JsonObject root = new()
        {
            ["$schema"] = SchemaUri,
            ["name"] = "Layered Install Test Mod",
            ["author"] = "Layered Install Tests",
            ["version"] = "1.0.0",
            ["targets"] = new JsonArray
            {
                new JsonObject
                {
                    ["file"] = "sharedassets0.assets",
                    ["patches"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "TextAsset",
                            ["match"] = new JsonObject { ["m_Script"] = "Ciallo～(∠・ω< )⌒★" },
                            ["set"] = new JsonObject
                            {
                                ["m_Name"] = new JsonObject
                                {
                                    ["from"] = from,
                                    ["to"] = to,
                                },
                            },
                        },
                    },
                },
            },
        };

        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static byte[] CreatePayloadManifest(string source)
    {
        JsonObject root = new()
        {
            ["$schema"] = SchemaUri,
            ["name"] = "Layered Install Test Mod",
            ["author"] = "Layered Install Tests",
            ["version"] = "1.0.0",
            ["copyFiles"] = new JsonArray
            {
                new JsonObject { ["source"] = source },
            },
            ["targets"] = new JsonArray
            {
                new JsonObject
                {
                    ["file"] = "sharedassets0.assets",
                    ["patches"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "TextAsset",
                            ["match"] = new JsonObject { ["m_Name"] = "Text" },
                            ["set"] = new JsonObject
                            {
                                ["m_Name"] = new JsonObject { ["from"] = "Text", ["to"] = "Text" },
                            },
                        },
                    },
                },
            },
        };

        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private sealed class LayeredInstallFixture : IDisposable
    {
        public string GameDirectory { get; }
        public string GameAssetsPath { get; }
        public string OtherAssetsPath { get; }
        public string PayloadPath { get; }

        public string BaseAssetsPath => Repository.BaseSnapshots.ResolveFilePath(
            GameInstanceIdentity.CreateFingerprint(FileSystem, GameDirectory),
            "Game_Data/sharedassets0.assets");

        public ICompositionRepository Repository { get; }
        public IWorkflowService WorkflowService { get; }

        private RepositoryService RepositoryService => _serviceProvider.GetRequiredService<RepositoryService>();
        private IFileSystemOperations FileSystem => _serviceProvider.GetRequiredService<IFileSystemOperations>();

        private readonly RepositoryTestDirectory _directory;
        private readonly ServiceProvider _serviceProvider;

        public LayeredInstallFixture(int repositoryVersion = 2)
        {
            _directory = new RepositoryTestDirectory();
            string repositoryDirectory = _directory.CreateDirectory("backup");
            GameDirectory = _directory.CreateDirectory("game");
            string gameDataDirectory = _directory.CreateDirectory("game", "Game_Data");
            GameAssetsPath = Path.Combine(gameDataDirectory, "sharedassets0.assets");
            OtherAssetsPath = Path.Combine(gameDataDirectory, "other.assets");
            PayloadPath = Path.Combine(gameDataDirectory, "config.txt");
            File.Copy(GetFixtureAssetsPath(), GameAssetsPath);
            File.Copy(GetFixtureAssetsPath(), OtherAssetsPath);

            if (repositoryVersion == 1)
            {
                _directory.WriteFile(
                    "backup/repository.json",
                    "{\"formatVersion\":1,\"repositoryId\":\"legacy-repository\"}");
            }

            ServiceCollection services = new();
            services.AddLogging();
            services.AddUnityAssetsPatcherApplication();
            services.AddUnityAssetsPatcherOperations();
            services.AddUnityAssetsPatcherInfrastructure(OpenClassPackage);
            services.AddUnityAssetsPatcherRepository(repositoryDirectory);
            _serviceProvider = services.BuildServiceProvider();
            Repository = _serviceProvider.GetRequiredService<ICompositionRepository>();
            WorkflowService = _serviceProvider.GetRequiredService<IWorkflowService>();
        }

        public InstallModResult Install(string packagePath)
        {
            OperationResult<InstallModResult> result = InstallOperation(
                new InstallRequest(packagePath, GameDirectory));

            return Assert.IsType<OperationSucceeded<InstallModResult>>(result).Value;
        }

        public InstallPreviewResult Preview(InstallRequest request)
        {
            using IServiceScope scope = CreateScope();
            IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
            OperationResult<InstallPreviewResult> result = dispatcher
                .DispatchAsync(new PreviewInstallRequest(request))
                .GetAwaiter()
                .GetResult();

            return Assert.IsType<OperationSucceeded<InstallPreviewResult>>(result).Value;
        }

        public OperationResult<InstallModResult> InstallOperation(InstallRequest request)
        {
            using IServiceScope scope = CreateScope();
            IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

            return dispatcher
                .DispatchAsync(new InstallModRequest(request))
                .GetAwaiter()
                .GetResult();
        }

        public UninstallPreviewResult PreviewUninstall(UninstallPreviewRequest request)
        {
            return Assert.IsType<OperationSucceeded<UninstallPreviewResult>>(
                PreviewUninstallOperation(request)).Value;
        }

        public OperationResult<UninstallPreviewResult> PreviewUninstallOperation(
            UninstallPreviewRequest request)
        {
            using IServiceScope scope = CreateScope();
            IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

            return dispatcher
                .DispatchAsync(request)
                .GetAwaiter()
                .GetResult();
        }

        public UninstallModResult Uninstall(UninstallModRequest request)
        {
            return Assert.IsType<OperationSucceeded<UninstallModResult>>(
                UninstallOperation(request)).Value;
        }

        public OperationResult<UninstallModResult> UninstallOperation(UninstallModRequest request)
        {
            using IServiceScope scope = CreateScope();
            IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

            return dispatcher
                .DispatchAsync(request)
                .GetAwaiter()
                .GetResult();
        }

        public IServiceScope CreateScope()
        {
            return _serviceProvider.CreateScope();
        }

        public RepositoryRecoveryPreview PreviewRecovery()
        {
            OperationResult<RepositoryRecoveryPreview> result =
                WorkflowService.PreviewPendingTransaction(GameDirectory);

            return Assert.IsType<OperationSucceeded<RepositoryRecoveryPreview>>(result).Value;
        }

        public RepositoryRecoveryReport Recover()
        {
            OperationResult<RepositoryRecoveryReport> result =
                WorkflowService.RecoverPendingTransactions(GameDirectory);

            return Assert.IsType<OperationSucceeded<RepositoryRecoveryReport>>(result).Value;
        }

        public void CreatePendingTransaction(
            string installId,
            RepositoryOperationKind kind,
            string beforeSource,
            string afterSource,
            bool applyAfter,
            bool moveLayerToRemoved = false)
        {
            string transactionDirectory = Repository.TransactionDirectory;
            string rollbackDirectory = Path.Combine(transactionDirectory, "rollback");
            string preparedDirectory = Path.Combine(transactionDirectory, "prepared");
            Directory.CreateDirectory(rollbackDirectory);
            Directory.CreateDirectory(preparedDirectory);

            string rollbackPath = Path.Combine(rollbackDirectory, "file-000000.bin");
            string preparedPath = Path.Combine(preparedDirectory, "file-000000.bin");
            File.Copy(beforeSource, rollbackPath);
            File.Copy(afterSource, preparedPath);

            const string relativePath = "Game_Data/sharedassets0.assets";
            FileIntegrity before = FileSystem.ComputeFileIntegrity(beforeSource);
            FileIntegrity after = FileSystem.ComputeFileIntegrity(afterSource);
            RepositoryTransaction transaction = new(
                RepositoryService.LoadMetadata().RepositoryId,
                kind,
                installId,
                GameInstanceIdentity.CreateFingerprint(FileSystem, GameDirectory),
                [
                    new RepositoryTransactionFile(
                        RepositoryFileKind.Assets,
                        relativePath,
                        before,
                        after,
                        Path.Combine("rollback", "file-000000.bin"),
                        Path.Combine("prepared", "file-000000.bin"))
                ]);
            RepositoryTransactionStore.Save(FileSystem, transactionDirectory, transaction);

            if (applyAfter)
            {
                File.Copy(afterSource, GameAssetsPath, overwrite: true);
            }

            if (moveLayerToRemoved)
            {
                string layerDirectory = Repository.Layers.GetLayerDirectory(installId);
                string removedDirectory = Path.Combine(transactionDirectory, "removed-install");
                FileSystem.MoveDirectory(layerDirectory, removedDirectory);
            }
        }

        public string CreatePackage(
            string name,
            byte[] manifest,
            params (string Path, byte[] Content)[] additionalEntries)
        {
            string packagePath = _directory.GetPath("packages", $"{name}.zip");
            string? directory = Path.GetDirectoryName(packagePath);

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream stream = File.Create(packagePath);
            using ZipArchive archive = new(stream, ZipArchiveMode.Create);
            ZipArchiveEntry entry = archive.CreateEntry("manifest.json");
            using (Stream output = entry.Open())
            {
                output.Write(manifest);
            }

            foreach ((string path, byte[] content) in additionalEntries)
            {
                ZipArchiveEntry payload = archive.CreateEntry(path);
                using Stream payloadOutput = payload.Open();
                payloadOutput.Write(content);
            }

            return packagePath;
        }

        public string ReadName(string path)
        {
            IAssetFileSessionFactory factory = _serviceProvider.GetRequiredService<IAssetFileSessionFactory>();
            using IAssetFileSession session = factory.Open(path);
            AssetField root = session.ReadField(new AssetPathId(4));
            AssetField name = FindField(root, "m_Name");
            AssetScalarField scalar = Assert.IsType<AssetScalarField>(name);
            AssetScalarValue.String value = Assert.IsType<AssetScalarValue.String>(scalar.Value);

            return value.Value;
        }

        private static AssetField FindField(AssetField field, string name)
        {
            Stack<AssetField> pending = new();
            pending.Push(field);

            while (pending.Count > 0)
            {
                AssetField current = pending.Pop();

                if (current.Name == name)
                {
                    return current;
                }

                foreach (AssetField child in current.Children.Reverse())
                {
                    pending.Push(child);
                }
            }

            throw new InvalidOperationException($"Asset field was not found: {name}");
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
            _directory.Dispose();
        }

        private static string GetFixtureAssetsPath()
        {
            return Path.Combine(AppContext.BaseDirectory, "Fixtures", "sharedassets0.assets");
        }

        private static Stream OpenClassPackage()
        {
            return File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "resources.tpk"));
        }
    }
}
