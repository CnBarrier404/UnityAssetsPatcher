using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Updates;
using Xunit;

namespace UnityAssetsPatcher.TUI.Tests;

public sealed class TerminalDependencyInjectionTests
{
    [Fact]
    public void AddUnityAssetsPatcherTUI_WhenProviderValidationIsEnabled_RegistersTerminalApp()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new AppInfo("Unity Assets Patcher", "dev"));

        services.AddSingleton<IUpdateCheckModule>(new StubUpdateCheckModule());

        services.AddSingleton<ILogger<TerminalApp>>(NullLogger<TerminalApp>.Instance);

        services.AddUnityAssetsPatcherTUI();

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var terminalApp = provider.GetRequiredService<TerminalApp>();

        Assert.NotNull(terminalApp);
    }

    private sealed class StubUpdateCheckModule : IUpdateCheckModule
    {
        public Task<OperationResult<UpdateInfo?>> CheckForUpdateAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<OperationResult<UpdateInfo?>>(
                new OperationSucceeded<UpdateInfo?>(null));
        }
    }
}
