using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Abstractions.Assets;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.CLI;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.Infrastructure;
using UnityAssetsPatcher.Tests.Support;
using Xunit;

namespace UnityAssetsPatcher.Tests.CLI;

public sealed class CLIApplicationTests : IDisposable
{
    private const string ValidManifest =
        """
        {
          "schemaVersion": 1,
          "name": "Example Mod",
          "author": "Example Author",
          "version": "1.0.0",
          "targets": [
            {
              "file": "sharedassets0.assets",
              "patches": [
                {
                  "type": "Camera",
                  "match": { "m_Name": "Main Camera" },
                  "set": {
                    "field of view": { "from": 60.0, "to": 75.0 }
                  }
                }
              ]
            }
          ]
        }
        """;

    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"UapCliTests.{Guid.NewGuid():N}");

    private readonly ServiceProvider _serviceProvider;

    public CLIApplicationTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var assetsFileService = new StubAssetsFileService([]);
        _serviceProvider = new ServiceCollection()
            .AddSingleton<IAssetsAccessScopeFactory>(assetsFileService)
            .AddUnityAssetsPatcherInfrastructure()
            .AddUnityAssetsPatcherApplication(Path.Combine(_temporaryDirectory, "backup"))
            .BuildServiceProvider();
    }

    [Fact]
    public void Run_CheckValidJson_IsSilentAndReturnsSuccess()
    {
        string manifestPath = WriteFile("custom.json", ValidManifest);
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp();

        int exitCode = app.Run(["check", "--config", manifestPath]);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_CheckValidJson_WithJsonFormatReturnsManifestSummary()
    {
        string manifestPath = WriteFile("custom.json", ValidManifest);
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp();

        int exitCode = app.Run(["check", "--config", manifestPath, "--format", "json"]);

        Assert.Equal(0, exitCode);
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("check", json.RootElement.GetProperty("command").GetString());
        Assert.Equal("Example Mod", json.RootElement.GetProperty("data").GetProperty("name").GetString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_CheckWithoutConfig_UsesCurrentDirectoryManifest()
    {
        WriteFile("manifest.json", ValidManifest);
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp();

        int exitCode = app.Run(["check"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_CheckValidZip_IsSilentAndReturnsSuccess()
    {
        string zipPath = Path.Combine(_temporaryDirectory, "mod.zip");
        using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("folder/MANIFEST.JSON");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(ValidManifest);
        }

        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp();

        int exitCode = app.Run(["check", "-c", zipPath]);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_CheckInvalidManifest_PrintsExceptionAndReturnsValidationFailure()
    {
        string manifestPath = WriteFile("invalid.json", "{}");
        (CLIApplication app, _, StringWriter error) = CreateApp();

        int exitCode = app.Run(["check", "-c", manifestPath]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Error [invalid_manifest]:", error.ToString());
        Assert.Contains("schemaVersion", error.ToString());
        Assert.DoesNotContain(" at ", error.ToString());
    }

    [Fact]
    public void Run_CheckMissingFile_PrintsExceptionAndReturnsValidationFailure()
    {
        (CLIApplication app, _, StringWriter error) = CreateApp();

        int exitCode = app.Run(["check", "-c", Path.Combine(_temporaryDirectory, "missing.json")]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Error [file_not_found]:", error.ToString());
        Assert.Contains("Manifest file not found", error.ToString());
    }

    [Theory]
    [InlineData("-c")]
    [InlineData("--unknown")]
    [InlineData("extra.json")]
    public void Run_CheckInvalidArguments_PrintsUsageAndReturnsUsageFailure(string argument)
    {
        (CLIApplication app, _, StringWriter error) = CreateApp();

        int exitCode = app.Run(["check", argument]);

        Assert.Equal(2, exitCode);
        Assert.Contains("check", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_CheckDuplicateConfig_PrintsUsageAndReturnsUsageFailure()
    {
        (CLIApplication app, _, StringWriter error) = CreateApp();

        int exitCode = app.Run(["check", "-c", "one.json", "--config", "two.json"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("-c", error.ToString());
        Assert.Contains("check", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Run_RootHelp_PrintsRegisteredCommands(string argument)
    {
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp();

        int exitCode = app.Run([argument]);

        Assert.Equal(0, exitCode);
        Assert.Contains("check", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_CheckHelp_PrintsOptions()
    {
        (CLIApplication app, StringWriter output, StringWriter error) = CreateApp();

        int exitCode = app.Run(["check", "--help"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("--config", output.ToString());
        Assert.Contains("-c", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_UnknownCommand_PrintsRootUsageHintAndReturnsUsageFailure()
    {
        (CLIApplication app, _, StringWriter error) = CreateApp();

        int exitCode = app.Run(["unknown"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("unknown", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("check", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        Directory.Delete(_temporaryDirectory, recursive: true);
    }

    private string WriteFile(string fileName, string contents)
    {
        string path = Path.Combine(_temporaryDirectory, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    private (CLIApplication App, StringWriter Output, StringWriter Error) CreateApp()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var options = new CLIOptions();
        var command = new CheckCLICommand(
            _serviceProvider.GetRequiredService<IWorkflowService>(),
            () => _temporaryDirectory,
            options);
        var app = new CLIApplication([command], output, error, options);

        return (app, output, error);
    }
}
