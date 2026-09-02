using UnityAssetsPatcher.Application;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.TUI.Pages.Settings;

public sealed class SettingsLogic
{
    public bool VerboseLogging => _runtimeConfig.VerboseLogging;

    private readonly AppRuntimeConfig _runtimeConfig;
    private readonly ILoggingLevelSwitch? _loggingLevelSwitch;

    public SettingsLogic(AppRuntimeConfig runtimeConfig, ILoggingLevelSwitch? loggingLevelSwitch = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeConfig);

        _runtimeConfig = runtimeConfig;
        _loggingLevelSwitch = loggingLevelSwitch;
    }

    public void SetVerboseLogging(bool enabled)
    {
        _runtimeConfig.VerboseLogging = enabled;

        _loggingLevelSwitch?.MinimumLevel = enabled ? LoggingLevel.Debug : LoggingLevel.Information;
    }
}
