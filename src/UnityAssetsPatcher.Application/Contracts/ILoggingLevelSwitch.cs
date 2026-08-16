namespace UnityAssetsPatcher.Application.Contracts;

public enum LoggingLevel
{
    Information,
    Debug
}

public interface ILoggingLevelSwitch
{
    public LoggingLevel MinimumLevel { get; set; }
}
