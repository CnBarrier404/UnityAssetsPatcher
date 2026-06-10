using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Tui.Pages;

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

        Context.Renderer.PrepareOutputArea();
        Context.UseFindWorkflow(workflow =>
        {
            var matches = workflow.Find(new FindAssetsRequest(assetsFilePath, configPath));
            Context.Renderer.WriteFindResults(matches);

            return 0;
        });

        return TerminalPageResult.ReturnToMenu();
    }
}
