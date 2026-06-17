using System.Globalization;
using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Pages;

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

        Context.Ui.Layout.PrepareOutputArea();
        Context.UseInspectWorkflow(workflow =>
        {
            InspectListResult result = workflow.List(new InspectListRequest(assetsFilePath, limit));
            WriteAssetSummary(result.Assets, result.TotalCount);

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

        Context.Ui.Layout.PrepareOutputArea();
        Context.UseInspectWorkflow(workflow =>
        {
            AssetsFieldInfo fieldTree = workflow.Fields(new InspectFieldsRequest(assetsFilePath, pathId));
            WriteAssetFields(fieldTree);

            return 0;
        });

        return TerminalPageResult.ReturnToMenu();
    }

    private bool TryReadAssetSummaryLimit(out int? limit)
    {
        while (true)
        {
            Context.Ui.Text.WriteBlankLine();
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
        Context.Ui.Lists.WriteChoiceList(InspectMenuChoices, selectedIndex);
    }

    private void WriteRowLimitMenu(int selectedIndex, bool clear)
    {
        NewPage("Rows to print", clear: clear);
        Context.Ui.Lists.WriteChoiceList(RowLimitChoices, selectedIndex);
    }

    private void WriteAssetSummary(IReadOnlyList<AssetsInfo> assets, int totalCount)
    {
        var table = Context.Ui.Tables.CreateTable();
        table.AddColumn(new TableColumn("Path ID").RightAligned());
        table.AddColumn(new TableColumn("Type ID").RightAligned());
        table.AddColumn("Type Name");
        table.AddColumn(new TableColumn("Byte Size").RightAligned());

        foreach (AssetsInfo asset in assets)
        {
            table.AddRow(
                asset.PathId.ToString(CultureInfo.InvariantCulture),
                asset.TypeId.ToString(CultureInfo.InvariantCulture),
                TerminalText.Escape(asset.TypeName),
                asset.ByteSize.ToString(CultureInfo.InvariantCulture));
        }

        Context.Console.Write(table);

        if (assets.Count >= totalCount)
        {
            return;
        }

        Context.Ui.Text.WriteBlankLine();
        Context.Ui.Text.WriteInfo($"Showing {assets.Count} of {totalCount} assets.");
    }

    private void WriteAssetFields(AssetsFieldInfo fieldTree)
    {
        WriteAssetField(fieldTree, 0);
    }

    private void WriteAssetField(AssetsFieldInfo field, int depth)
    {
        string indentation = new(' ', depth * 2);
        string value = field.Value is null ? string.Empty : $": {field.Value}";
        Context.Console.MarkupLine(
            $"{indentation}{TerminalText.Escape(field.Name)} ({TerminalText.Escape(field.TypeName)}){TerminalText.Escape(value)}");

        foreach (AssetsFieldInfo child in field.Children)
        {
            WriteAssetField(child, depth + 1);
        }
    }
}
