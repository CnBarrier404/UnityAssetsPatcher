using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Tui;

internal sealed class InspectTerminalPage
{
    private const int DefaultAssetSummaryLimit = 100;

    private readonly TerminalAppContext _context;
    private readonly InteractivePrompts _prompts;

    public InspectTerminalPage(TerminalAppContext context, InteractivePrompts prompts)
    {
        _context = context;
        _prompts = prompts;
    }

    public void Run()
    {
        while (true)
        {
            WriteMenu();

            string? choice = _prompts.ReadRawLine();

            if (choice is null)
            {
                return;
            }

            switch (choice.Trim().ToLowerInvariant())
            {
                case "1":
                    RunList();
                    return;
                case "2":
                    RunFields();
                    return;
                case "3":
                    return;
                default:
                    _context.Output.WriteLine("Invalid option. Enter 1, 2, or 3.");
                    break;
            }
        }
    }

    private void WriteMenu()
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine("Inspect assets");
        _context.Output.WriteLine();
        _context.Output.WriteLine("1. List assets");
        _context.Output.WriteLine("2. Show asset fields");
        _context.Output.WriteLine("3. Back");
        _context.Output.WriteLine();
        _context.Output.Write("Select an option: ");
    }

    private void RunList()
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine("List assets");
        _context.Output.WriteLine();

        string? assetsFilePath = _prompts.ReadExistingFilePath("Assets file path");

        if (assetsFilePath is null || !TryReadAssetSummaryLimit(out int? limit))
        {
            return;
        }

        _context.Output.WriteLine();
        _context.UseService(service =>
        {
            var assets = service.InspectList(new InspectListRequest(assetsFilePath, limit));
            TerminalOutputFormatter.WriteAssetSummary(_context.Output, assets, limit);

            return 0;
        });
    }

    private bool TryReadAssetSummaryLimit(out int? limit)
    {
        while (true)
        {
            _context.Output.WriteLine();
            _context.Output.WriteLine("Rows to print:");
            _context.Output.WriteLine($"1. First {DefaultAssetSummaryLimit}");
            _context.Output.WriteLine("2. All rows");
            _context.Output.WriteLine("3. Custom limit");
            _context.Output.WriteLine("4. Back");
            _context.Output.WriteLine();
            _context.Output.Write("Select an option: ");

            string? choice = _prompts.ReadRawLine();

            if (choice is null)
            {
                limit = null;

                return false;
            }

            switch (choice.Trim().ToLowerInvariant())
            {
                case "1":
                    limit = DefaultAssetSummaryLimit;
                    return true;
                case "2":
                    limit = null;
                    return true;
                case "3":
                    if (_prompts.TryReadPositiveInt("Maximum rows", out int customLimit))
                    {
                        limit = customLimit;
                        return true;
                    }

                    limit = null;
                    return false;
                case "4":
                    limit = null;
                    return false;
                default:
                    _context.Output.WriteLine("Invalid option. Enter 1, 2, 3, or 4.");
                    break;
            }
        }
    }

    private void RunFields()
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine("Show asset fields");
        _context.Output.WriteLine();

        string? assetsFilePath = _prompts.ReadExistingFilePath("Assets file path");

        if (assetsFilePath is null || !_prompts.TryReadInt64("Path ID", out long pathId))
        {
            return;
        }

        _context.Output.WriteLine();
        _context.UseService(service =>
        {
            AssetsFieldInfo fieldTree = service.InspectFields(new InspectFieldsRequest(assetsFilePath, pathId));
            TerminalOutputFormatter.WriteAssetFields(_context.Output, fieldTree);

            return 0;
        });
    }
}
