using System.Text.Json;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.CLI;
using Xunit;

namespace UnityAssetsPatcher.Tests.CLI;

public sealed class CLICommandSetTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(), $"UapCliCommandSetTests.{Guid.NewGuid():N}");

    public CLICommandSetTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [Fact]
    public void Run_InspectListDefaultsToFirst100AndReportsTotal()
    {
        var assets = Enumerable.Range(1, 105)
            .Select(id => new UnityAssetsPatcher.Domain.Assets.AssetInfo(id, "Camera"))
            .ToArray();
        var workflow = new StubWorkflowService
        {
            InspectAssets = assets,
        };
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp(workflow);

        int exitCode = app.Run(["inspect", "list", Path.Combine(_temporaryDirectory, "data.assets")]);

        Assert.Equal(0, exitCode);
        Assert.Equal(100, workflow.LastInspectListRequest!.Limit);
        Assert.Contains("Path ID", output.ToString());
        Assert.Contains("Showing 100 of 105 assets.", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_InspectFieldsJsonWritesRecursiveFieldTree()
    {
        var workflow = new StubWorkflowService
        {
            InspectFieldTree = new UnityAssetsPatcher.Domain.Assets.AssetField(
                "Camera",
                "Camera",
                null,
                [
                    new UnityAssetsPatcher.Domain.Assets.AssetField("field of view", "float",
                        new UnityAssetsPatcher.Domain.Assets.AssetFieldValue.Float(90f), [])
                ]),
        };
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp(workflow);

        int exitCode = app.Run([
            "inspect", "fields", Path.Combine(_temporaryDirectory, "data.assets"), "4", "--format", "json",
        ]);

        Assert.Equal(0, exitCode);
        Assert.Equal(4, workflow.LastInspectFieldsRequest!.PathId);
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        JsonElement fieldTree = json.RootElement.GetProperty("data").GetProperty("fieldTree");
        Assert.Equal("Camera", fieldTree.GetProperty("name").GetString());
        Assert.Equal("field of view", fieldTree.GetProperty("children")[0].GetProperty("name").GetString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_InspectListAllPassesNoLimit()
    {
        var workflow = new StubWorkflowService
        {
            InspectAssets = [new UnityAssetsPatcher.Domain.Assets.AssetInfo(1, "Camera")],
        };
        (CLIApplication app, _, StringWriter error) = CreateApp(workflow);

        int exitCode = app.Run([
            "inspect", "list", Path.Combine(_temporaryDirectory, "data.assets"), "--all",
        ]);

        Assert.Equal(0, exitCode);
        Assert.Null(workflow.LastInspectListRequest!.Limit);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Theory]
    [InlineData("--limit", "0")]
    [InlineData("--limit", "-1")]
    [InlineData("--all", "--limit", "2")]
    public void Run_InspectListInvalidLimitOptionsReturnUsageFailure(params string[] options)
    {
        var workflow = new StubWorkflowService();
        (CLIApplication app, _, StringWriter error) = CreateApp(workflow);
        var arguments = new List<string>
        {
            "inspect", "list", Path.Combine(_temporaryDirectory, "data.assets"),
        };
        arguments.AddRange(options);

        int exitCode = app.Run(arguments);

        Assert.Equal(2, exitCode);
        Assert.Null(workflow.LastInspectListRequest);
        Assert.Contains(options[0], error.ToString());
    }

    [Fact]
    public void Run_InstallPreview_MapsPathsAndRepeatedOptionalGroups()
    {
        var workflow = new StubWorkflowService
        {
            InstallPreviewResult = PreviewResult(),
        };
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp(workflow);
        string packagePath = Path.Combine(_temporaryDirectory, "mod.zip");
        string gameDirectory = Path.Combine(_temporaryDirectory, "game");

        int exitCode = app.Run([
            "install", "preview",
            "--package", packagePath,
            "--game-directory", gameDirectory,
            "--optional-group", "Audio",
            "-o", "Textures",
        ]);

        Assert.Equal(0, exitCode);
        Assert.Equal(Path.GetFullPath(packagePath), workflow.LastInstallRequest!.ZipFilePath);
        Assert.Equal(Path.GetFullPath(gameDirectory), workflow.LastInstallRequest.GameDirectory);
        Assert.Equal(["Audio", "Textures"], workflow.LastInstallRequest.SelectedOptionalGroups);
        Assert.Contains("Preview: Example Mod 1.0.0", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_InstallApplyWithoutYes_ReturnsUsageFailureWithoutCallingWorkflow()
    {
        var workflow = new StubWorkflowService();
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp(workflow);

        int exitCode = app.Run([
            "install", "apply", "--package", Path.Combine(_temporaryDirectory, "mod.zip"),
            "--format", "json",
        ]);

        Assert.Equal(2, exitCode);
        Assert.Null(workflow.LastInstallRequest);
        Assert.Equal(string.Empty, output.ToString());
        using JsonDocument json = JsonDocument.Parse(error.ToString());
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("install.apply", json.RootElement.GetProperty("command").GetString());
        Assert.Equal("usage_error", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("--yes", json.RootElement.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public void Run_InstallApplyJson_ReturnsStableInstallId()
    {
        var workflow = new StubWorkflowService
        {
            InstallResult = new InstallModResult("0123456789abcdef", "Example Mod", "1.0.0", [], [], EmptyTiming()),
        };
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp(workflow);

        int exitCode = app.Run([
            "install", "apply", "-p", Path.Combine(_temporaryDirectory, "mod.zip"), "--yes", "--format", "json",
        ]);

        Assert.Equal(0, exitCode);
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("install.apply", json.RootElement.GetProperty("command").GetString());
        Assert.Equal("0123456789abcdef",
            json.RootElement.GetProperty("data").GetProperty("installId").GetString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_UninstallListJson_UsesIdsWithoutExposingBackupDirectories()
    {
        var workflow = new StubWorkflowService
        {
            InstalledMods =
            [
                new InstallRecordSummary(
                    "install-id",
                    "Example Mod",
                    "1.0.0",
                    "Example Game",
                    DateTimeOffset.Parse("2026-07-15T01:02:03Z")),
            ],
        };
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp(workflow);

        int exitCode = app.Run(["uninstall", "list", "--format", "json"]);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("backup", output.ToString(), StringComparison.OrdinalIgnoreCase);
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        JsonElement mod = json.RootElement.GetProperty("data").GetProperty("mods")[0];
        Assert.Equal("install-id", mod.GetProperty("installId").GetString());
        Assert.Equal("Example Mod", mod.GetProperty("name").GetString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_UninstallPreviewBlocked_IsSuccessfulPreview()
    {
        var workflow = new StubWorkflowService
        {
            UninstallPreviewResult = new UninstallPreviewResult(
                "install-id",
                "Example Mod",
                "1.0.0",
                DateTimeOffset.UtcNow,
                _temporaryDirectory,
                false,
                [new UninstallBlockingModResult("Later Mod", "2.0.0", DateTimeOffset.UtcNow, ["data.assets"])],
                [],
                []),
        };
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp(workflow);

        int exitCode = app.Run(["uninstall", "preview", "--id", "install-id", "--format", "json"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("install-id", workflow.LastUninstallPreviewRequest!.InstallId);
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        Assert.False(json.RootElement.GetProperty("data").GetProperty("canUninstall").GetBoolean());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_UninstallApply_RequiresYesAndMapsId()
    {
        var workflow = new StubWorkflowService
        {
            UninstallResult = new UninstallModResult("install-id", "Example Mod", "1.0.0", [], []),
        };
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp(workflow);

        int exitCode = app.Run(["uninstall", "apply", "--id", "install-id", "--yes"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("install-id", workflow.LastUninstallRequest!.InstallId);
        Assert.Contains("Uninstalled: Example Mod 1.0.0", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_BusinessFailureInJsonMode_WritesMachineReadableError()
    {
        var workflow = new StubWorkflowService
        {
            Failure = new InvalidOperationException("unsafe operation", new IOException("file changed")),
        };
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp(workflow);

        int exitCode = app.Run(["uninstall", "list", "--format", "json"]);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        using JsonDocument json = JsonDocument.Parse(error.ToString());
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("uninstall.list", json.RootElement.GetProperty("command").GetString());
        Assert.Equal("command_failed", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("file changed",
            json.RootElement.GetProperty("error").GetProperty("causes")[0].GetProperty("message").GetString());
    }

    [Fact]
    public void Run_StructuredFailureInJsonMode_WritesStableCodeAndParameters()
    {
        var workflow = new StubWorkflowService
        {
            Error = new OperationError(OperationErrorCode.FileIntegrityMismatch)
            {
                Parameters = new Dictionary<string, string> { ["path"] = "data.assets" },
            },
        };
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp(workflow);

        int exitCode = app.Run(["uninstall", "list", "--format", "json"]);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        using JsonDocument json = JsonDocument.Parse(error.ToString());
        JsonElement jsonError = json.RootElement.GetProperty("error");
        Assert.Equal("file_integrity_mismatch", jsonError.GetProperty("code").GetString());
        Assert.Equal("data.assets", jsonError.GetProperty("parameters").GetProperty("path").GetString());
    }

    [Fact]
    public void RecoveryPreview_PrintsEveryPlannedFileAction()
    {
        var workflow = new StubWorkflowService
        {
            RecoveryPreview = new BackupRecoveryPreview(
                BackupRepositoryStatus.RecoveryRequired, _temporaryDirectory, "install", "id",
                BackupRecoveryPlanAction.RollBack, true,
                [new BackupRecoveryFileChange("mod.bin", BackupRecoveryFileAction.Delete)], []),
        };
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp(workflow);

        int exitCode = app.Run(["recovery", "preview", "--game-directory", _temporaryDirectory]);

        Assert.Equal(0, exitCode);
        Assert.Contains("delete: mod.bin", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void RecoveryApply_RequiresExplicitConfirmation()
    {
        (CLIApplication app, _, StringWriter error) = CreateApp(new StubWorkflowService());

        int exitCode = app.Run(["recovery", "apply", "--game-directory", _temporaryDirectory]);

        Assert.Equal(2, exitCode);
        Assert.Contains("--yes", error.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory)) Directory.Delete(_temporaryDirectory, recursive: true);
    }

    private (CLIApplication App, StringWriter Output, StringWriter Error) CreateApp(
        StubWorkflowService workflow)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var options = new CLIOptions();
        ICLICommand[] commands =
        [
            new InspectCLICommand(workflow, options),
            new InstallCLICommand(workflow, options),
            new UninstallCLICommand(workflow, options),
            new RecoveryCLICommand(workflow, options),
        ];
        return (new CLIApplication(commands, output, error, options), output, error);
    }

    private static InstallPreviewResult PreviewResult()
    {
        return new InstallPreviewResult(
            "Example Mod",
            "1.0.0",
            "Example Author",
            [],
            [("Audio", "Audio files")],
            EmptyTiming());
    }

    private static TimingSnapshot EmptyTiming() => new([], TimeSpan.Zero);

    private sealed class StubWorkflowService : IWorkflowService
    {
        public OperationResult<BackupRecoveryReport> CheckPendingTransactions() =>
            new OperationSucceeded<BackupRecoveryReport>(BackupRecoveryReport.Clean);

        public InstallPreviewResult? InstallPreviewResult { get; init; }
        public InstallModResult? InstallResult { get; init; }
        public IReadOnlyList<InstallRecordSummary> InstalledMods { get; init; } = [];
        public UninstallPreviewResult? UninstallPreviewResult { get; init; }
        public UninstallModResult? UninstallResult { get; init; }
        public Exception? Failure { get; init; }
        public OperationError? Error { get; init; }
        public BackupRecoveryPreview? RecoveryPreview { get; init; }
        public IReadOnlyList<UnityAssetsPatcher.Domain.Assets.AssetInfo> InspectAssets { get; init; } = [];
        public UnityAssetsPatcher.Domain.Assets.AssetField? InspectFieldTree { get; init; }
        public InspectListRequest? LastInspectListRequest { get; private set; }
        public InspectFieldsRequest? LastInspectFieldsRequest { get; private set; }
        public InstallRequest? LastInstallRequest { get; private set; }
        public UninstallPreviewRequest? LastUninstallPreviewRequest { get; private set; }
        public UninstallModRequest? LastUninstallRequest { get; private set; }

        public OperationResult<BackupRecoveryPreview> PreviewPendingTransaction(string gameDirectory) =>
            new OperationSucceeded<BackupRecoveryPreview>(RecoveryPreview ??
                                                          new BackupRecoveryPreview(BackupRepositoryStatus.Clean, null,
                                                              null, null, null, false, [], []));

        public OperationResult<BackupRecoveryReport> RecoverPendingTransactions(string gameDirectory)
        {
            ThrowIfConfigured();
            return new OperationSucceeded<BackupRecoveryReport>(BackupRecoveryReport.Clean);
        }

        public OperationResult<ModManifest> CheckManifest(string path) => throw new NotSupportedException();

        public OperationResult<InspectListResult> InspectList(InspectListRequest request)
        {
            LastInspectListRequest = request;
            ThrowIfConfigured();
            IEnumerable<UnityAssetsPatcher.Domain.Assets.AssetInfo> listed = request.Limit is null
                ? InspectAssets
                : InspectAssets.Take(request.Limit.Value);
            var result = new InspectListResult(
                listed.Select(asset => new InspectAssetSummary(asset.PathId, asset.TypeName, $"Name{asset.PathId}"))
                    .ToArray(),
                InspectAssets.Count);

            return new OperationSucceeded<InspectListResult>(result);
        }

        public OperationResult<UnityAssetsPatcher.Domain.Assets.AssetField> InspectFields(InspectFieldsRequest request)
        {
            LastInspectFieldsRequest = request;
            ThrowIfConfigured();
            return new OperationSucceeded<UnityAssetsPatcher.Domain.Assets.AssetField>(
                InspectFieldTree ?? throw new InvalidOperationException("Field tree was not configured."));
        }

        public OperationResult<InstallPreviewResult> PreviewInstall(InstallRequest request)
        {
            LastInstallRequest = request;
            ThrowIfConfigured();
            return new OperationSucceeded<InstallPreviewResult>(InstallPreviewResult ??
                                                                throw new InvalidOperationException(
                                                                    "Preview result was not configured."));
        }

        public OperationResult<InstallModResult> Install(InstallRequest request)
        {
            LastInstallRequest = request;
            ThrowIfConfigured();
            return new OperationSucceeded<InstallModResult>(InstallResult ??
                                                            throw new InvalidOperationException(
                                                                "Install result was not configured."));
        }

        public OperationResult<IReadOnlyList<InstallRecordSummary>> ListInstalledMods()
        {
            ThrowIfConfigured();
            if (Error is not null)
            {
                return new OperationFailed<IReadOnlyList<InstallRecordSummary>>(Error);
            }

            return new OperationSucceeded<IReadOnlyList<InstallRecordSummary>>(InstalledMods);
        }

        public OperationResult<UninstallPreviewResult> PreviewUninstall(UninstallPreviewRequest request)
        {
            LastUninstallPreviewRequest = request;
            ThrowIfConfigured();
            return new OperationSucceeded<UninstallPreviewResult>(UninstallPreviewResult ??
                                                                  throw new InvalidOperationException(
                                                                      "Uninstall preview result was not configured."));
        }

        public OperationResult<UninstallModResult> Uninstall(UninstallModRequest request)
        {
            LastUninstallRequest = request;
            ThrowIfConfigured();
            return new OperationSucceeded<UninstallModResult>(UninstallResult ??
                                                              throw new InvalidOperationException(
                                                                  "Uninstall result was not configured."));
        }

        private void ThrowIfConfigured()
        {
            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }
}
