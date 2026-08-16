using Serilog.Core;
using Serilog.Events;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Logging;

internal sealed class SerilogLoggingLevelSwitch : ILoggingLevelSwitch
{
    public LoggingLevel MinimumLevel
    {
        get
        {
            switch (_levelSwitch.MinimumLevel)
            {
                case LogEventLevel.Information:
                    return LoggingLevel.Information;
                case LogEventLevel.Debug:
                    return LoggingLevel.Debug;
                case LogEventLevel.Verbose:
                case LogEventLevel.Warning:
                case LogEventLevel.Error:
                case LogEventLevel.Fatal:
                default:
                    throw new InvalidOperationException(
                        $"Unsupported Serilog minimum level: {_levelSwitch.MinimumLevel}.");
            }
        }
        set => _levelSwitch.MinimumLevel = value switch
        {
            LoggingLevel.Information => LogEventLevel.Information,
            LoggingLevel.Debug => LogEventLevel.Debug,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported logging level.")
        };
    }

    private readonly LoggingLevelSwitch _levelSwitch;

    public SerilogLoggingLevelSwitch(LoggingLevelSwitch levelSwitch)
    {
        ArgumentNullException.ThrowIfNull(levelSwitch);

        _levelSwitch = levelSwitch;
    }
}
