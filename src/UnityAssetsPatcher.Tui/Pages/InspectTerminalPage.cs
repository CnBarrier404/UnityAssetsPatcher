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

    public InspectTerminalPage(TerminalAppContext context)
        : base(context) { }

    public override string Title => "Inspect assets";

    public override string Description => "List assets or inspect a selected asset field tree.";

    public override TerminalPageResult Run()
    {
        string choice = Context.Prompts.ReadChoice(
            InspectMenuChoices,
            Cancel,
            WriteInspectMenu);

        return choice switch
        {
            ListAssets => RunList(),
            ShowAssetFields => RunFields(),
            _ => TerminalPageResult.ReturnToMenu(false),
        };
    }

    private TerminalPageResult RunList()
    {
        NewPage("List assets", "Print an asset summary for one assets file.");

        string? assetsFilePath = Context.Prompts.ReadExistingFilePath("Assets file path");

        if (assetsFilePath is null || !TryReadAssetSummaryLimit(out int? limit))
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        Context.Renderer.PrepareOutputArea();
        Context.UseInspectWorkflow(workflow =>
        {
            InspectListResult result = workflow.List(new InspectListRequest(assetsFilePath, limit));
            Context.Renderer.WriteAssetSummary(result.Assets, result.TotalCount);

            return 0;
        });

        return TerminalPageResult.ReturnToMenu();
    }

    private TerminalPageResult RunFields()
    {
        NewPage("Show asset fields", "Print the field tree for one selected Path ID.");

        string? assetsFilePath = Context.Prompts.ReadExistingFilePath("Assets file path");

        if (assetsFilePath is null || !Context.Prompts.TryReadInt64("Path ID", out long pathId))
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        Context.Renderer.PrepareOutputArea();
        Context.UseInspectWorkflow(workflow =>
        {
            AssetsFieldInfo fieldTree = workflow.Fields(new InspectFieldsRequest(assetsFilePath, pathId));
            Context.Renderer.WriteAssetFields(fieldTree);

            return 0;
        });

        return TerminalPageResult.ReturnToMenu();
    }

    private bool TryReadAssetSummaryLimit(out int? limit)
    {
        while (true)
        {
            Context.Renderer.WriteBlankLine();
            string choice = Context.Prompts.ReadChoice(
                RowLimitChoices,
                Cancel,
                WriteRowLimitMenu);

            switch (choice)
            {
                case DefaultAssetSummaryLimitChoice:
                    limit = DefaultAssetSummaryLimit;
                    return true;
                case "All rows":
                    limit = null;
                    return true;
                case "Custom limit":
                    if (Context.Prompts.TryReadPositiveInt("Maximum rows", out int customLimit))
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

    private void WriteInspectMenu(int selectedIndex, bool clear)
    {
        NewPage(Title, "List assets or inspect the field tree for a selected Path ID.", clear: clear);
        Context.Renderer.WriteChoiceList(InspectMenuChoices, selectedIndex);
    }

    private void WriteRowLimitMenu(int selectedIndex, bool clear)
    {
        NewPage("Rows to print", clear: clear);
        Context.Renderer.WriteChoiceList(RowLimitChoices, selectedIndex);
    }
}
