using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.Domain.Assets;
using Xunit;

namespace UnityAssetsPatcher.CLI.Tests;

public sealed class UninstallCLIOutputTests
{
    [Fact]
    public async Task RunAsync_WhenUninstallCanProceed_ReportsRecompositionCount()
    {
        var workflow = new StubWorkflowService
        {
            UninstallPreview = new UninstallPreviewResult(
                "layer-1",
                "Test Mod",
                "1.0.0",
                DateTimeOffset.UnixEpoch,
                "C:\\Game",
                true,
                [],
                [
                    new UninstallChangedFileResult(
                        "Game_Data/sharedassets0.assets",
                        UninstallChangedFileAction.Rebuild,
                        FileIntegrityStatus.Matches),
                    new UninstallChangedFileResult(
                        "Game_Data/config.txt",
                        UninstallChangedFileAction.RestoreBase,
                        FileIntegrityStatus.Matches),
                    new UninstallChangedFileResult(
                        "Game_Data/old.dll",
                        UninstallChangedFileAction.Delete,
                        FileIntegrityStatus.Matches),
                ]),
        };
        (CLIApplication application, StringWriter output) = CreateApplication(workflow);

        int exitCode = await application.RunAsync(
            ["uninstall", "preview", "--id", "layer-1", "--game-directory", "C:\\Game"],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("Can uninstall: yes; 3 file(s) will be recomposed or restored.", output.ToString());
    }

    [Fact]
    public async Task RunAsync_WhenUninstallHasDependency_ReportsDetailedDiagnostic()
    {
        var workflow = new StubWorkflowService
        {
            UninstallPreview = new UninstallPreviewResult(
                "layer-1",
                "Target Mod",
                "1.0.0",
                DateTimeOffset.UnixEpoch,
                "C:\\Game",
                false,
                [
                    new UninstallDependencyFailureResult(
                        "Dependent Mod",
                        "2.0.0",
                        "Game_Data/sharedassets0.assets",
                        new PatchDiagnostic(
                            PatchDiagnosticCode.ValueMismatch,
                            "C:\\Game\\Game_Data\\sharedassets0.assets",
                            42,
                            "m_Name",
                            "Layer One",
                            "External Value"))
                ],
                []),
        };
        (CLIApplication application, StringWriter output) = CreateApplication(workflow);

        int exitCode = await application.RunAsync(
            ["uninstall", "preview", "--id", "layer-1", "--game-directory", "C:\\Game"],
            TestContext.Current.CancellationToken);

        string text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("Can uninstall: no; real patch dependencies were found.", text);
        Assert.Contains("Real patch dependencies:", text);
        Assert.Contains("Dependent Mod 2.0.0 at Game_Data/sharedassets0.assets", text);
        Assert.Contains("valueMismatch", text);
        Assert.Contains("field=m_Name", text);
        Assert.Contains("expected=Layer One", text);
        Assert.Contains("actual=External Value", text);
    }

    private static (CLIApplication Application, StringWriter Output) CreateApplication(
        StubWorkflowService workflow)
    {
        var output = new StringWriter();
        var options = new CLIOptions();
        var application = new CLIApplication(
            [new UninstallCLICommand(workflow, options)],
            output,
            new StringWriter(),
            options);

        return (application, output);
    }

    private sealed class StubWorkflowService : IWorkflowService
    {
        public UninstallPreviewResult? UninstallPreview { get; init; }

        public OperationResult<RepositoryRecoveryReport> CheckPendingTransactions() =>
            throw new NotSupportedException();

        public OperationResult<RepositoryRecoveryPreview> PreviewPendingTransaction(string gameDirectory) =>
            throw new NotSupportedException();

        public OperationResult<RepositoryRecoveryReport> RecoverPendingTransactions(string gameDirectory) =>
            throw new NotSupportedException();

        public OperationResult<InspectListResult> InspectList(InspectListRequest request) =>
            throw new NotSupportedException();

        public OperationResult<AssetField> InspectFields(InspectFieldsRequest request) =>
            throw new NotSupportedException();

        public OperationResult<IReadOnlyList<InstallRecordSummary>> ListInstalledMods() =>
            throw new NotSupportedException();

        public OperationResult<UninstallPreviewResult> PreviewUninstall(UninstallPreviewRequest request)
        {
            return UninstallPreview is null
                ? throw new InvalidOperationException("Uninstall preview was not configured.")
                : new OperationSucceeded<UninstallPreviewResult>(UninstallPreview);
        }

        public OperationResult<UninstallModResult> Uninstall(UninstallModRequest request) =>
            throw new NotSupportedException();
    }
}
