using System.Globalization;
using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class FindTerminalPage : TerminalPage
{
    public FindTerminalPage(TerminalAppContext context) : base(context) { }

    public override string Title => "Find assets";

    public override string Description => "Search assets using manifest include rules.";

    public override TerminalPageResult Run()
    {
        NewPage(Title, "Find assets matching manifest include conditions.");

        string? assetsFilePath = Context.Prompts.ReadExistingFilePath("Assets file path");

        if (assetsFilePath is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        string? configPath = Context.Prompts.ReadExistingFilePath("Manifest JSON or mod zip path");

        if (configPath is null)
        {
            return TerminalPageResult.ReturnToMenu(false);
        }

        Context.Ui.Layout.PrepareOutputArea();
        Context.UseFindWorkflow(workflow =>
        {
            var matches = workflow.Find(new FindAssetsRequest(assetsFilePath, configPath));
            WriteFindResults(matches);

            return 0;
        });

        return TerminalPageResult.ReturnToMenu();
    }

    private void WriteFindResults(IReadOnlyList<AssetMatch> matches)
    {
        var table = Context.Ui.Tables.CreateTable();
        table.AddColumn(new TableColumn("Path ID").RightAligned());
        table.AddColumn(new TableColumn("Type ID").RightAligned());
        table.AddColumn("Type Name");
        table.AddColumn("Matched Fields");

        foreach (AssetMatch match in matches)
        {
            string matchedFields = string.Join(", ",
                match.IncludeGroup.Select(condition =>
                    $"{condition.Key}={JsonUtils.FormatElementValue(condition.Value)}"));
            table.AddRow(
                match.Asset.PathId.ToString(CultureInfo.InvariantCulture),
                match.Asset.TypeId.ToString(CultureInfo.InvariantCulture),
                TerminalText.Escape(match.Asset.TypeName),
                TerminalText.Escape(matchedFields));
        }

        Context.Console.Write(table);
    }
}
