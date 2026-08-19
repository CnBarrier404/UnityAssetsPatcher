using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.IO;
using UnityAssetsPatcher.Infrastructure;
using UnityAssetsPatcher.Infrastructure.IO;
using Xunit;

namespace UnityAssetsPatcher.CLI.Tests;

public sealed class RepositoryCLICommandTests : IDisposable
{
    private readonly string _temporaryDirectory;
    private ServiceProvider? _serviceProvider;

    public RepositoryCLICommandTests()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"uap-repository-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [Fact]
    public async Task RunAsync_WhenConfirmationIsMissing_ReturnsUsageErrorWithoutClearingRepository()
    {
        string metadataPath = WriteUnsupportedRepository();
        (CLIApplication application, _, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(
            ["repository", "clear"],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Contains("Required option '--yes' was not provided.", error.ToString());
        Assert.True(File.Exists(metadataPath));
    }

    [Fact]
    public async Task RunAsync_WhenRepositoryFormatIsUnsupported_ClearsAndInitializesCurrentFormat()
    {
        WriteUnsupportedRepository();
        string legacyPath = WriteFile("backup/installed/legacy/record.json", "legacy");
        (CLIApplication application, StringWriter output, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(
            ["repository", "clear", "--yes"],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("Cleared unsupported backup repository format 1 and initialized format 2.", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
        Assert.False(File.Exists(legacyPath));

        JsonNode metadata = JsonNode.Parse(File.ReadAllText(RepositoryMetadataPath()))!;
        Assert.Equal(2, metadata["formatVersion"]!.GetValue<int>());
        Assert.Matches("^[0-9a-f]{32}$", metadata["repositoryId"]!.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_WhenJsonOutputIsRequested_WritesStructuredClearResult()
    {
        WriteUnsupportedRepository();
        (CLIApplication application, StringWriter output, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(
            ["repository", "clear", "--yes", "--format", "Json"],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        JsonNode envelope = JsonNode.Parse(output.ToString())!;
        Assert.True(envelope["success"]!.GetValue<bool>());
        Assert.Equal("repository.clear", envelope["command"]!.GetValue<string>());
        Assert.Equal(1, envelope["data"]!["previousFormatVersion"]!.GetValue<int>());
        Assert.Equal(2, envelope["data"]!["formatVersion"]!.GetValue<int>());
    }

    [Fact]
    public async Task RunAsync_WhenRepositoryFormatIsCurrent_RejectsWithoutDeletingContents()
    {
        WriteFile(
            "backup/repository.json",
            "{\"formatVersion\":2,\"repositoryId\":\"current-repository\"}");
        string markerPath = WriteFile("backup/marker.txt", "keep");
        (CLIApplication application, StringWriter output, StringWriter error) = CreateApplication();

        int exitCode = await application.RunAsync(
            ["repository", "clear", "--yes"],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Error [backup.clear_not_allowed]", error.ToString());
        Assert.Equal("keep", File.ReadAllText(markerPath));
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
        services.AddUnityAssetsPatcherRepository(Path.Combine(_temporaryDirectory, "backup"));
        services.AddUnityAssetsPatcherApplication();
        services.AddUnityAssetsPatcherOperations();
        services.AddSingleton<CLIOptions>();
        _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        var output = new StringWriter();
        var error = new StringWriter();
        var command = new RepositoryCLICommand(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<CLIOptions>());
        var application = new CLIApplication(
            [command],
            output,
            error,
            _serviceProvider.GetRequiredService<CLIOptions>());

        return (application, output, error);
    }

    private string WriteUnsupportedRepository()
    {
        return WriteFile("backup/repository.json", "{\"formatVersion\":1}");
    }

    private string RepositoryMetadataPath()
    {
        return Path.Combine(_temporaryDirectory, "backup", "repository.json");
    }

    private string WriteFile(string relativePath, string contents)
    {
        string path = Path.Combine(_temporaryDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);

        return path;
    }
}
