using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Manifests;
using UnityAssetsPatcher.Application.Packages;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Infrastructure.IO;
using UnityAssetsPatcher.Infrastructure.Packages;
using Xunit;

namespace UnityAssetsPatcher.CLI.Tests;

public sealed class CliApplicationTests : IDisposable
{
    private readonly string _temporaryDirectory;

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

    public CliApplicationTests()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"uap-cli-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_temporaryDirectory);
    }

    [Fact]
    public async Task RunAsync_WhenCheckUsesDefaultManifest_ReturnsSuccessWithoutOutput()
    {
        File.WriteAllText(Path.Combine(_temporaryDirectory, "manifest.json"), ValidManifest);

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
        using (ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("manifest.json");

            using StreamWriter writer = new(entry.Open());

            writer.Write(ValidManifest);
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

        File.WriteAllText(manifestPath, "{}");

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
        var workflow = new CheckManifestWorkflow(sourceReader, NullLogger<CheckManifestWorkflow>.Instance);
        var output = new StringWriter();
        var error = new StringWriter();
        var command = new CheckCLICommand(workflow, () => _temporaryDirectory, error);
        var application = new CLIApplication([command], output, error);

        return (application, output, error);
    }
}
