using System.CommandLine;
using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.CLI;

public sealed class CheckCLICommand : ICLICommand
{
    public Command Command { get; }

    private readonly IWorkflowService _workflowService;
    private readonly Func<string> _getCurrentDirectory;
    private readonly CLIOptions _options;
    private readonly Option<string?> _configOption;

    public CheckCLICommand(
        IWorkflowService workflowService,
        Func<string> getCurrentDirectory,
        CLIOptions? options = null)
    {
        _workflowService = workflowService;
        _getCurrentDirectory = getCurrentDirectory;
        _options = options ?? new CLIOptions();
        _configOption = new Option<string?>("--config", "-c")
        {
            Description = "Manifest JSON or mod ZIP path (default: ./manifest.json).",
        };

        Command = new Command("check", "Validate a mod manifest.");
        Command.Options.Add(_configOption);
        Command.SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        string configPath = parseResult.GetValue(_configOption) ??
                            Path.Combine(_getCurrentDirectory(), "manifest.json");

        try
        {
            ModManifest manifest = _workflowService.CheckManifest(configPath);
            return CLIOutput.WriteSuccess(
                parseResult,
                _options,
                "check",
                CLIOutput.ManifestSummary(configPath, manifest),
                _ => { });
        }
        catch (Exception exception)
        {
            return CLIOutput.WriteFailure(parseResult, _options, "check", exception);
        }
    }
}
