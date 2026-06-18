namespace UnityAssetsPatcher.TUI;

public sealed class TerminalInstallOptions
{
    public string BackupDirectory { get; }

    public TerminalInstallOptions(string backupDirectory)
    {
        BackupDirectory = backupDirectory;
    }
}
