using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Application.Mods;
using UnityAssetsPatcher.Infrastructure.IO;
using UnityAssetsPatcher.Infrastructure.Mods;
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
    public async Task RunAsync_WhenManifestJsonIsMalformed_WritesManifestFailure()
    {
        string manifestPath = Path.Combine(_temporaryDirectory, "malformed.json");

        await File.WriteAllTextAsync(manifestPath, "{", TestContext.Current.CancellationToken);

        (CLIApplication application, StringWriter output, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(
            ["check", "-c", manifestPath],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.StartsWith(
            $"Error [manifest.invalid_json]: The mod manifest is invalid.{Environment.NewLine}",
            error.ToString());
        Assert.Contains("byte_position: 1", error.ToString());
        Assert.Contains("line_number: 0", error.ToString());
    }

    [Fact]
    public async Task RunAsync_WhenPackageManifestIsMissing_WritesPackageFailure()
    {
        string packagePath = Path.Combine(_temporaryDirectory, "missing-manifest.zip");
        await using (ZipArchive archive =
                     await ZipFile.OpenAsync(packagePath, ZipArchiveMode.Create, TestContext.Current.CancellationToken))
        {
            archive.CreateEntry("payload.bin");
        }

        (CLIApplication application, StringWriter output, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(
            ["check", "-c", packagePath],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.StartsWith(
            $"Error [mod_package.missing_manifest]: The mod package is invalid.{Environment.NewLine}",
            error.ToString());
        Assert.Contains($"package_path: {packagePath}", error.ToString());
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
        Directory.Delete(_temporaryDirectory, true);
    }

    private (CLIApplication Application, StringWriter Output, StringWriter Error) CreateApplication()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFileSystemOperations>(provider => new FileSystemOperations(
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileSystemOperations>>()));
        services.AddSingleton<IModArchiveReader, ZipModArchiveReader>();
        services.AddUnityAssetsPatcherApplication();
        _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
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
