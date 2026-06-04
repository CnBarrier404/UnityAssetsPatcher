using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.Cli;

public sealed class CommandContext
{
    public CommandContext(AssetsWorkflowService service, ConsoleOutputFormatter formatter, string backupDirectory,
        TextWriter output, TextWriter error)
    {
        Service = service;
        Formatter = formatter;
        BackupDirectory = backupDirectory;
        Output = output;
        Error = error;
    }

    public AssetsWorkflowService Service { get; }
    public ConsoleOutputFormatter Formatter { get; }
    public string BackupDirectory { get; }
    public TextWriter Output { get; }
    public TextWriter Error { get; }
}
