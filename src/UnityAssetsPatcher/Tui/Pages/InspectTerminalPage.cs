using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Tui.Pages;

internal sealed class InspectTerminalPage : TerminalPage
{
    private const int DefaultAssetSummaryLimit = 100;
    private const string DefaultAssetSummaryLimitChoice = "First 100";
    private const string ListAssets = "List assets";
    private const string ShowAssetFields = "Show asset fields";
    private const string Cancel = "__cancel";

    private static readonly string[] InspectMenuChoices =
    [
        ListAssets,
        ShowAssetFields,
    ];

    private static readonly string[] RowLimitChoices =
    [
        DefaultAssetSummaryLimitChoice,
        "All rows",
        "Custom limit",
    ];

    private readonly InteractivePrompts _prompts;

    public InspectTerminalPage(TerminalAppContext context, InteractivePrompts prompts)
        : base(context)
    {
        _prompts = prompts;
    }

    public override string Title => "Inspect assets";

    public override string Description => "List assets or inspect a selected asset field tree.";

    public override bool Run()
    {
        NewPage(Title, "List assets or inspect the field tree for a selected Path ID.");
        string choice = _prompts.ReadSubMenuChoice(string.Empty, InspectMenuChoices, Cancel);

        return choice switch
        {
            ListAssets => RunList(),
            ShowAssetFields => RunFields(),
            _ => false,
        };
    }

    private bool RunList()
    {
        NewPage("List assets", "Print an asset summary for one assets file.");

        string? assetsFilePath = _prompts.ReadExistingFilePath("Assets file path");

        if (assetsFilePath is null || !TryReadAssetSummaryLimit(out int? limit))
        {
            return false;
        }

        TerminalOutputFormatter.WriteBlankLine(Context.Console);
        Context.UseInspectWorkflow(workflow =>
        {
            var assets = workflow.List(new InspectListRequest(assetsFilePath, limit));
            TerminalOutputFormatter.WriteAssetSummary(Context.Console, assets, limit);

            return 0;
        });

        return true;
    }

    private bool RunFields()
    {
        NewPage("Show asset fields", "Print the field tree for one selected Path ID.");

        string? assetsFilePath = _prompts.ReadExistingFilePath("Assets file path");

        if (assetsFilePath is null || !_prompts.TryReadInt64("Path ID", out long pathId))
        {
            return false;
        }

        TerminalOutputFormatter.WriteBlankLine(Context.Console);
        Context.UseInspectWorkflow(workflow =>
        {
            AssetsFieldInfo fieldTree = workflow.Fields(new InspectFieldsRequest(assetsFilePath, pathId));
            TerminalOutputFormatter.WriteAssetFields(Context.Console, fieldTree);

            return 0;
        });

        return true;
    }

    private bool TryReadAssetSummaryLimit(out int? limit)
    {
        while (true)
        {
            TerminalOutputFormatter.WriteBlankLine(Context.Console);
            string choice = _prompts.ReadSubMenuChoice("Rows to print", RowLimitChoices, Cancel);

            switch (choice)
            {
                case DefaultAssetSummaryLimitChoice:
                    limit = DefaultAssetSummaryLimit;
                    return true;
                case "All rows":
                    limit = null;
                    return true;
                case "Custom limit":
                    if (_prompts.TryReadPositiveInt("Maximum rows", out int customLimit))
                    {
                        limit = customLimit;
                        return true;
                    }

                    limit = null;
                    return false;
                case Cancel:
                    limit = null;
                    return false;
            }
        }
    }
}
