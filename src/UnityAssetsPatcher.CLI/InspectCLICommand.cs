using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Features.Inspect;
using UnityAssetsPatcher.Application.Messaging;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Domain.Assets;

namespace UnityAssetsPatcher.CLI;

public sealed class InspectCLICommand : ICLICommand
{
    private const int DefaultLimit = 100;

    public Command Command { get; }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CLIOptions _options;

    public InspectCLICommand(IServiceScopeFactory scopeFactory, CLIOptions options)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);

        _scopeFactory = scopeFactory;
        _options = options;

        Command = new Command("inspect", "Inspect assets file contents.");
        Command.Subcommands.Add(CreateListCommand());
        Command.Subcommands.Add(CreateFieldsCommand());
    }

    private Command CreateListCommand()
    {
        var assetsFile = AssetsFileArgument();
        var all = new Option<bool>("--all")
        {
            Description = "Print every asset summary row."
        };
        var limit = new Option<int?>("--limit")
        {
            Description = "Maximum number of asset summary rows to print."
        };
        var command = new Command("list", "Print asset summary rows.");
        command.Arguments.Add(assetsFile);
        command.Options.Add(all);
        command.Options.Add(limit);
        command.Validators.Add(result =>
        {
            bool printAll = result.GetValue(all);
            int? rowLimit = result.GetValue(limit);

            if (printAll && result.GetResult(limit) is { Implicit: false })
            {
                result.AddError("--all and --limit cannot be used together.");
            }
            else if (rowLimit <= 0)
            {
                result.AddError("--limit must be greater than 0.");
            }
        });
        command.SetAction((parseResult, cancellationToken) => ExecuteList(
            parseResult,
            parseResult.GetRequiredValue(assetsFile),
            parseResult.GetValue(all) ? null : parseResult.GetValue(limit) ?? DefaultLimit,
            cancellationToken));

        return command;
    }

    private Command CreateFieldsCommand()
    {
        var assetsFile = AssetsFileArgument();
        var pathId = new Argument<long>("path-id")
        {
            Description = "Asset Path ID to inspect."
        };
        var command = new Command("fields", "Print the field tree for one asset.");
        command.Arguments.Add(assetsFile);
        command.Arguments.Add(pathId);
        command.SetAction((parseResult, cancellationToken) => ExecuteFields(
            parseResult,
            parseResult.GetRequiredValue(assetsFile),
            parseResult.GetRequiredValue(pathId),
            cancellationToken));

        return command;
    }

    private async Task<int> ExecuteList(
        ParseResult parseResult,
        string assetsFilePath,
        int? limit,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(assetsFilePath);
        using IServiceScope scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var result = await dispatcher
            .DispatchAsync<InspectListRequest, OperationResult<InspectListResult>>(
                new InspectListRequest(fullPath, limit),
                cancellationToken)
            .ConfigureAwait(false);

        return CLIOutput.WriteResult(
            parseResult,
            _options,
            "inspect.list",
            result,
            value => CLIOutput.InspectList(fullPath, value),
            CLIOutput.WriteInspectListText);
    }

    private async Task<int> ExecuteFields(
        ParseResult parseResult,
        string assetsFilePath,
        long pathId,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(assetsFilePath);
        using IServiceScope scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var result = await dispatcher
            .DispatchAsync<InspectFieldsRequest, OperationResult<AssetField>>(
                new InspectFieldsRequest(fullPath, pathId),
                cancellationToken)
            .ConfigureAwait(false);

        return CLIOutput.WriteResult(
            parseResult,
            _options,
            "inspect.fields",
            result,
            value => CLIOutput.InspectFields(fullPath, pathId, value),
            CLIOutput.WriteInspectFieldsText);
    }

    private static Argument<string> AssetsFileArgument()
    {
        return new Argument<string>("assets-file")
        {
            Description = "Path to the Unity assets file."
        };
    }
}
