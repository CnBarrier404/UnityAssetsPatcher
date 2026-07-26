using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Extensions.Logging;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Logging;

internal sealed class SerilogLoggingLevelSwitch(LoggingLevelSwitch levelSwitch) : ILoggingLevelSwitch
{
    public LogLevel MinimumLevel
    {
        get => LevelConvert.ToExtensionsLevel(levelSwitch.MinimumLevel);
        set => levelSwitch.MinimumLevel = LevelConvert.ToSerilogLevel(value);
    }
}
