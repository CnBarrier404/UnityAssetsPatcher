namespace UnityAssetsPatcher.Tui;

public sealed class TerminalSettings
{
    public bool VerboseLogging { get; set; }
    public bool InstallTimingDetails { get; set; }
}

internal sealed record TerminalSettingDisplay(
    string Name,
    string Description,
    bool Enabled);
