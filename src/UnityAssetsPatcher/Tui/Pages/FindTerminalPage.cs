using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Tui.Pages;

internal sealed class FindTerminalPage : TerminalPage
{
    private readonly InteractivePrompts _prompts;

    public FindTerminalPage(TerminalAppContext context, InteractivePrompts prompts) : base(context)
    {
        _prompts = prompts;
    }

    public override string Title => "Find assets";

    public override string Description => "Search assets using manifest include rules.";

    public override bool Run()
    {
        NewPage(Title, "Find assets matching manifest include conditions.");

        string? assetsFilePath = _prompts.ReadExistingFilePath("Assets file path");

        if (assetsFilePath is null)
        {
            return false;
        }

        string? configPath = _prompts.ReadExistingFilePath("Manifest JSON or mod zip path");

        if (configPath is null)
        {
            return false;
        }

        TerminalOutputFormatter.WriteBlankLine(Context.Console);
        TerminalOutputFormatter.ClearBottomFooterArea(Context.Console);
        Context.UseFindWorkflow(workflow =>
        {
            var matches = workflow.Find(new FindAssetsRequest(assetsFilePath, configPath));
            TerminalOutputFormatter.WriteFindResults(Context.Console, matches);

            return 0;
        });

        return true;
    }
}
