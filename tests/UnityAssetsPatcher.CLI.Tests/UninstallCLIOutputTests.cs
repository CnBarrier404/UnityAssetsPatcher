using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using Xunit;

namespace UnityAssetsPatcher.CLI.Tests;

public sealed class UninstallCLIOutputTests
{
    [Fact]
    public async Task RunAsync_WhenListingInstalledMods_WritesInstalledMods()
    {
        TestApplication test = CreateApplication();
        using (test.Services)
        {
            int exitCode = await test.Application.RunAsync(
                ["uninstall", "list"],
                TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.Contains("layer-1 | Test Mod 1.0.0 | Test Game", test.Output.ToString());
        }
    }

    [Fact]
    public async Task RunAsync_WhenUninstallCanProceed_ReportsRecompositionCount()
    {
        var preview = new UninstallPreviewResult(
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
            ]);
        TestApplication test = CreateApplication(preview);
        using (test.Services)
        {
            int exitCode = await test.Application.RunAsync(
                ["uninstall", "preview", "--id", "layer-1", "--game-directory", "C:\\Game"],
                TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.Contains("Can uninstall: yes; 3 file(s) will be recomposed or restored.", test.Output.ToString());
        }
    }

    [Fact]
    public async Task RunAsync_WhenUninstallHasDependency_ReportsDetailedDiagnostic()
    {
        var preview = new UninstallPreviewResult(
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
            []);
        TestApplication test = CreateApplication(preview);
        using (test.Services)
        {
            int exitCode = await test.Application.RunAsync(
                ["uninstall", "preview", "--id", "layer-1", "--game-directory", "C:\\Game"],
                TestContext.Current.CancellationToken);

            string text = test.Output.ToString();
            Assert.Equal(0, exitCode);
            Assert.Contains("Can uninstall: no; real patch dependencies were found.", text);
            Assert.Contains("Real patch dependencies:", text);
            Assert.Contains("Dependent Mod 2.0.0 at Game_Data/sharedassets0.assets", text);
            Assert.Contains("valueMismatch", text);
            Assert.Contains("field=m_Name", text);
            Assert.Contains("expected=Layer One", text);
            Assert.Contains("actual=External Value", text);
        }
    }

    private static TestApplication CreateApplication(UninstallPreviewResult? preview = null)
    {
        var output = new StringWriter();
        var options = new CLIOptions();
        var services = new ServiceCollection();
        services.AddScoped<IRequestDispatcher, RequestDispatcher>();
        if (preview is not null)
        {
            services.AddScoped<
                IRequestHandler<UninstallPreviewRequest, OperationResult<UninstallPreviewResult>>>(_ =>
                new StubUninstallPreviewHandler(preview));
        }

        services.AddScoped<
            IRequestHandler<ListInstalledModsRequest, OperationResult<IReadOnlyList<InstallRecordSummary>>>>(_ =>
            new StubInstalledModsHandler());
        ServiceProvider provider = services.BuildServiceProvider();
        var application = new CLIApplication(
            [
                new UninstallCLICommand(
                    provider.GetRequiredService<IServiceScopeFactory>(),
                    options)
            ],
            output,
            new StringWriter(),
            options);

        return new TestApplication(application, output, provider);
    }

    private sealed record TestApplication(
        CLIApplication Application,
        StringWriter Output,
        ServiceProvider Services);

    private sealed class StubUninstallPreviewHandler :
        IRequestHandler<UninstallPreviewRequest, OperationResult<UninstallPreviewResult>>
    {
        private readonly UninstallPreviewResult _preview;

        public StubUninstallPreviewHandler(UninstallPreviewResult preview)
        {
            _preview = preview;
        }

        public Task<OperationResult<UninstallPreviewResult>> HandleAsync(
            UninstallPreviewRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<OperationResult<UninstallPreviewResult>>(
                new OperationSucceeded<UninstallPreviewResult>(_preview));
        }
    }

    private sealed class StubInstalledModsHandler :
        IRequestHandler<ListInstalledModsRequest, OperationResult<IReadOnlyList<InstallRecordSummary>>>
    {
        public Task<OperationResult<IReadOnlyList<InstallRecordSummary>>> HandleAsync(
            ListInstalledModsRequest request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<InstallRecordSummary> installed =
            [
                new InstallRecordSummary(
                    "layer-1",
                    "Test Mod",
                    "1.0.0",
                    "Test Game",
                    DateTimeOffset.UnixEpoch),
            ];

            return Task.FromResult<OperationResult<IReadOnlyList<InstallRecordSummary>>>(
                new OperationSucceeded<IReadOnlyList<InstallRecordSummary>>(installed));
        }
    }
}
