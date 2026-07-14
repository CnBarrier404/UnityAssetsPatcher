using System.CommandLine;
using UnityAssetsPatcher.Application.Manifests;

namespace UnityAssetsPatcher.CLI;

public sealed class CheckCLICommand : ICLICommand
{
    public Command Command { get; }

    private readonly ModManifestReader _manifestReader;
    private readonly Func<string> _getCurrentDirectory;
    private readonly Option<string?> _configOption;

    public CheckCLICommand(ModManifestReader manifestReader, Func<string> getCurrentDirectory)
    {
        _manifestReader = manifestReader;
        _getCurrentDirectory = getCurrentDirectory;
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
            _manifestReader.Load(configPath);

            return 0;
        }
        catch (Exception exception)
        {
            WriteException(parseResult.InvocationConfiguration.Error, exception);

            return 1;
        }
    }

    private static void WriteException(TextWriter error, Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            string prefix = ReferenceEquals(current, exception) ? string.Empty : "Caused by ";
            error.WriteLine($"{prefix}{current.GetType().Name}: {current.Message}");
        }
    }
}
