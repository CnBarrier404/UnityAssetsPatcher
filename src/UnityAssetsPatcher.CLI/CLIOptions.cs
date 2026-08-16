using System.CommandLine;

namespace UnityAssetsPatcher.CLI;

public enum CLIOutputFormat
{
    Text,
    Json
}

public sealed class CLIOptions
{
    public Option<CLIOutputFormat> Format { get; } = new("--format")
    {
        Description = "Output format.",
        DefaultValueFactory = _ => CLIOutputFormat.Text,
        Recursive = true
    };
}
