using Microsoft.Extensions.Logging;

namespace UnityAssetsPatcher.Application.Contracts;

public interface ILoggingLevelSwitch
{
    public LogLevel MinimumLevel { get; set; }
}
