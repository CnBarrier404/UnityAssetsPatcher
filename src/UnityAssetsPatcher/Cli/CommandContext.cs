using UnityAssetsPatcher.Core;

namespace UnityAssetsPatcher.Cli;

public sealed class CommandContext
{
    public CommandContext(AssetsWorkflowService service, ConsoleOutputFormatter formatter, TextWriter output,
        TextWriter error)
    {
        Service = service;
        Formatter = formatter;
        Output = output;
        Error = error;
    }

    public AssetsWorkflowService Service { get; }
    public ConsoleOutputFormatter Formatter { get; }
    public TextWriter Output { get; }
    public TextWriter Error { get; }
}
