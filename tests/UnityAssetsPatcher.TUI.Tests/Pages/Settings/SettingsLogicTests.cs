using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Pages.Settings;
using Xunit;

namespace UnityAssetsPatcher.TUI.Tests.Pages.Settings;

public sealed class SettingsLogicTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerboseLogging_WhenRead_ReflectsRuntimeConfig(bool enabled)
    {
        var runtimeConfig = new AppRuntimeConfig
        {
            VerboseLogging = enabled
        };
        var logic = new SettingsLogic(runtimeConfig);

        bool result = logic.VerboseLogging;

        Assert.Equal(enabled, result);
    }

    [Theory]
    [InlineData(false, LoggingLevel.Information)]
    [InlineData(true, LoggingLevel.Debug)]
    public void SetVerboseLogging_WhenCalled_UpdatesRuntimeConfigAndLoggingLevel(
        bool enabled,
        LoggingLevel expectedLevel)
    {
        var runtimeConfig = new AppRuntimeConfig
        {
            VerboseLogging = !enabled
        };
        var loggingLevelSwitch = new StubLoggingLevelSwitch();
        var logic = new SettingsLogic(runtimeConfig, loggingLevelSwitch);

        logic.SetVerboseLogging(enabled);

        Assert.Equal(enabled, runtimeConfig.VerboseLogging);
        Assert.Equal(expectedLevel, loggingLevelSwitch.MinimumLevel);
    }

    private sealed class StubLoggingLevelSwitch : ILoggingLevelSwitch
    {
        public LoggingLevel MinimumLevel { get; set; }
    }
}
