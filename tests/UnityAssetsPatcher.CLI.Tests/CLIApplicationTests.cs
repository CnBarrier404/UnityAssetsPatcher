using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Features.Check;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Infrastructure.IO;
using UnityAssetsPatcher.Infrastructure.Packages;
using Xunit;

namespace UnityAssetsPatcher.CLI.Tests;

public sealed class CLIApplicationTests : IDisposable
{
    private readonly string _temporaryDirectory;
    private ServiceProvider? _serviceProvider;

    private const string ValidManifest = """
                                         {
                                           "$schema": "https://uap.cnbarrier.com/schema-v1.json",
                                           "name": "Test Mod",
                                           "author": "Test Author",
                                           "version": "1.0.0",
                                           "targets": [
                                             {
                                               "file": "sharedassets0.assets",
                                               "patches": [
                                                 {
                                                   "type": "Camera",
                                                   "match": { "m_Name": "Main" }
                                                 }
                                               ]
                                             }
                                           ]
                                         }
                                         """;

    public CLIApplicationTests()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"uap-cli-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_temporaryDirectory);
    }

    [Fact]
    public async Task RunAsync_WhenCheckUsesDefaultManifest_ReturnsSuccessWithoutOutput()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_temporaryDirectory, "manifest.json"),
            ValidManifest,
            TestContext.Current.CancellationToken);

        (CLIApplication application, StringWriter output, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(["check"], TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_WhenCheckUsesZip_ReturnsSuccessWithoutOutput()
    {
        string packagePath = Path.Combine(_temporaryDirectory, "mod.zip");
        await using (ZipArchive archive =
                     await ZipFile.OpenAsync(packagePath, ZipArchiveMode.Create, TestContext.Current.CancellationToken))
        {
            ZipArchiveEntry entry = archive.CreateEntry("manifest.json");

            await using StreamWriter writer = new(await entry.OpenAsync(TestContext.Current.CancellationToken));

            await writer.WriteAsync(ValidManifest);
        }

        (CLIApplication application, StringWriter output, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(
            ["check", "--config", packagePath],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_WhenManifestIsInvalid_WritesTextFailure()
    {
        string manifestPath = Path.Combine(_temporaryDirectory, "invalid.json");

        await File.WriteAllTextAsync(manifestPath, "{}", TestContext.Current.CancellationToken);

        (CLIApplication application, StringWriter output, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(
            ["check", "-c", manifestPath],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(
            $"Error [manifest.missing_property]: Required manifest property '$schema' is missing.{Environment.NewLine}",
            error.ToString());
    }

    [Fact]
    public async Task RunAsync_WhenArgumentsAreInvalid_ReturnsUsageErrorAndWritesHelp()
    {
        (CLIApplication application, StringWriter output, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(
            ["check", "--unknown"],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("check", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--unknown", error.ToString());
    }

    [Fact]
    public async Task RunAsync_WhenFormatOptionIsSpecified_ReturnsUsageError()
    {
        (CLIApplication application, _, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(
            ["check", "--format", "Json"],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Contains("--format", error.ToString());
    }

    [Fact]
    public async Task RunAsync_WhenCheckHelpIsRequested_ListsOnlyTextCommandOptions()
    {
        (CLIApplication application, StringWriter output, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(
            ["check", "--help"],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("--config", output.ToString());
        Assert.DoesNotContain("--format", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        Directory.Delete(_temporaryDirectory, recursive: true);
    }

    private (CLIApplication Application, StringWriter Output, StringWriter Error) CreateApplication()
    {
        var fileSystem = new FileSystemOperations(NullLogger<FileSystemOperations>.Instance);
        var archiveFactory = new ModPackageArchiveFactory(
            fileSystem,
            NullLogger<ModPackageArchiveFactory>.Instance);
        var archiveService = new ModPackageArchiveService(archiveFactory, fileSystem);
        var sourceReader = new ManifestSourceReader(archiveService, fileSystem);
        var handler = new CheckManifestHandler(sourceReader, NullLogger<CheckManifestHandler>.Instance);
        var services = new ServiceCollection();
        services.AddScoped<IRequestDispatcher, RequestDispatcher>();
        services.AddScoped<IRequestHandler<CheckManifestRequest, OperationResult<CheckManifestResult>>>(_ => handler);
        _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
        });
        var output = new StringWriter();
        var error = new StringWriter();
        var command = new CheckCLICommand(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            () => _temporaryDirectory,
            error);
        var application = new CLIApplication([command], output, error);

        return (application, output, error);
    }
}
