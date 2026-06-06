using UnityAssetsPatcher.Application.Contracts;

namespace UnityAssetsPatcher.Tui;

internal sealed class FindTerminalPage
{
    private readonly TerminalAppContext _context;
    private readonly InteractivePrompts _prompts;

    public FindTerminalPage(TerminalAppContext context, InteractivePrompts prompts)
    {
        _context = context;
        _prompts = prompts;
    }

    public void Run()
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine("Find assets");
        _context.Output.WriteLine();

        string? assetsFilePath = _prompts.ReadExistingFilePath("Assets file path");

        if (assetsFilePath is null)
        {
            return;
        }

        string? configPath = _prompts.ReadExistingFilePath("Manifest JSON or mod zip path");

        if (configPath is null)
        {
            return;
        }

        _context.Output.WriteLine();
        _context.UseService(service =>
        {
            var matches = service.FindAssets(new FindAssetsRequest(assetsFilePath, configPath));
            TerminalOutputFormatter.WriteFindResults(_context.Output, matches);

            return 0;
        });
    }
}
