using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application;
using Xunit;

namespace UnityAssetsPatcher.TUI.Tests;

public sealed class TerminalDependencyInjectionTests
{
    [Fact]
    public void AddUnityAssetsPatcherTUI_WhenProviderValidationIsEnabled_RegistersTerminalApp()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new AppRuntimeConfig());

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
}
