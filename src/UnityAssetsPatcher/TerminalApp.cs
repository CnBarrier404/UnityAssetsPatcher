using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher;

public sealed class TerminalApp
{
    private const int DefaultAssetSummaryLimit = 100;

    private readonly TerminalAppContext _context;
    private readonly InteractivePrompts _prompts;

    public TerminalApp(IAssetsFileService assetsFileService, TextReader input, TextWriter output, TextWriter error)
        : this(assetsFileService, Path.Combine(AppContext.BaseDirectory, "backup"), input, output, error) { }

    public TerminalApp(
        IAssetsFileService assetsFileService,
        string backupDirectory,
        TextReader input,
        TextWriter output,
        TextWriter error)
    {
        _context = new TerminalAppContext(
            CreateServiceScopeFactory(assetsFileService),
            backupDirectory,
            output,
            error);
        _prompts = new InteractivePrompts(input, output);
    }

    public int Run()
    {
        try
        {
            while (true)
            {
                WriteMainMenu();

                string? choice = _prompts.ReadRawLine();

                if (choice is null)
                {
                    return 0;
                }

                switch (choice.Trim().ToLowerInvariant())
                {
                    case "1":
                        RunMenuAction(() => RunInstall(apply: true));
                        break;
                    case "2":
                        RunMenuAction(() => RunInstall(apply: false));
                        break;
                    case "3":
                        RunMenuAction(RunInspect);
                        break;
                    case "4":
                        RunMenuAction(RunFind);
                        break;
                    case "5":
                        RunMenuAction(RunPatch);
                        break;
                    case "6":
                    case "q":
                    case "quit":
                    case "exit":
                        return 0;
                    default:
                        _context.Output.WriteLine("Invalid option. Enter 1, 2, 3, 4, 5, or 6.");
                        break;
                }

                _context.Output.WriteLine();
            }
        }
        catch (Exception exception)
        {
            _context.Error.WriteLine(exception.Message);

            return 1;
        }
    }

    private void WriteMainMenu()
    {
        _context.Output.WriteLine("========================================");
        _context.Output.WriteLine("        Unity Assets Patcher");
        _context.Output.WriteLine("========================================");
        _context.Output.WriteLine();
        _context.Output.WriteLine("1. Install a mod");
        _context.Output.WriteLine("2. Preview a mod install");
        _context.Output.WriteLine("3. Inspect assets");
        _context.Output.WriteLine("4. Find assets");
        _context.Output.WriteLine("5. Patch assets");
        _context.Output.WriteLine("6. Exit");
        _context.Output.WriteLine();
        _context.Output.Write("Select an option: ");
    }

    private void RunMenuAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            _context.Error.WriteLine(exception.Message);
        }
    }

    private void RunInstall(bool apply)
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine(apply ? "Install a mod" : "Preview a mod install");
        _context.Output.WriteLine();

        string? zipFilePath = _prompts.ReadExistingFilePath("Mod zip path");

        if (zipFilePath is null)
        {
            return;
        }

        string? gameDirectory = _prompts.ReadExistingDirectoryPath("Game directory");

        if (gameDirectory is null)
        {
            return;
        }

        _context.Output.WriteLine();
        _context.Output.WriteLine("Analyzing mod...");
        _context.Output.WriteLine();

        _context.UseService(service =>
        {
            InstallPreviewResult preview =
                service.PreviewInstallMod(new InstallPreviewRequest(zipFilePath, gameDirectory));
            TerminalOutputFormatter.WriteInstallPreview(_context.Output, preview);

            return 0;
        });

        if (!apply)
        {
            return;
        }

        _context.Output.WriteLine();

        if (!_prompts.Confirm("Apply these changes? [y/N]"))
        {
            _context.Output.WriteLine("Install canceled.");

            return;
        }

        _context.Output.WriteLine();
        _context.UseService(service =>
        {
            InstallModResult result = service.InstallMod(
                new InstallModRequest(zipFilePath, gameDirectory, _context.BackupDirectory));
            TerminalOutputFormatter.WriteInstallResult(_context.Output, result);

            return 0;
        });
    }

    private void RunInspect()
    {
        while (true)
        {
            WriteInspectMenu();

            string? choice = _prompts.ReadRawLine();

            if (choice is null)
            {
                return;
            }

            switch (choice.Trim().ToLowerInvariant())
            {
                case "1":
                    RunInspectList();
                    return;
                case "2":
                    RunInspectFields();
                    return;
                case "3":
                case "b":
                case "back":
                case "q":
                case "quit":
                case "exit":
                    return;
                default:
                    _context.Output.WriteLine("Invalid option. Enter 1, 2, or 3.");
                    break;
            }
        }
    }

    private void WriteInspectMenu()
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

    private void RunInspectList()
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
                case "b":
                case "back":
                case "q":
                case "quit":
                case "exit":
                    limit = null;
                    return false;
                default:
                    _context.Output.WriteLine("Invalid option. Enter 1, 2, 3, or 4.");
                    break;
            }
        }
    }

    private void RunInspectFields()
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

    private void RunFind()
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

    private void RunPatch()
    {
        while (true)
        {
            WritePatchMenu();

            string? choice = _prompts.ReadRawLine();

            if (choice is null)
            {
                return;
            }

            switch (choice.Trim().ToLowerInvariant())
            {
                case "1":
                    RunPatchPreview();
                    return;
                case "2":
                    RunPatchApply();
                    return;
                case "3":
                case "b":
                case "back":
                case "q":
                case "quit":
                case "exit":
                    return;
                default:
                    _context.Output.WriteLine("Invalid option. Enter 1, 2, or 3.");
                    break;
            }
        }
    }

    private void WritePatchMenu()
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine("Patch assets");
        _context.Output.WriteLine();
        _context.Output.WriteLine("1. Preview patch");
        _context.Output.WriteLine("2. Apply patch");
        _context.Output.WriteLine("3. Back");
        _context.Output.WriteLine();
        _context.Output.Write("Select an option: ");
    }

    private void RunPatchPreview()
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine("Preview patch");
        _context.Output.WriteLine();

        if (!TryReadPatchInputs(out string assetsFilePath, out string configPath))
        {
            return;
        }

        _context.Output.WriteLine();
        WritePatchPreview(assetsFilePath, configPath);
    }

    private void RunPatchApply()
    {
        _context.Output.WriteLine();
        _context.Output.WriteLine("Apply patch");
        _context.Output.WriteLine();

        if (!TryReadPatchInputs(out string assetsFilePath, out string configPath) ||
            !_prompts.TryReadOptionalPath("Output path (blank to overwrite input)", out string? outputPath))
        {
            return;
        }

        _context.Output.WriteLine();
        WritePatchPreview(assetsFilePath, configPath);

        _context.Output.WriteLine();

        if (!_prompts.Confirm("Apply these changes? [y/N]"))
        {
            _context.Output.WriteLine("Patch canceled.");

            return;
        }

        _context.Output.WriteLine();
        _context.UseService(service =>
        {
            PatchApplyResult result = service.ApplyPatch(
                new PatchApplyRequest(assetsFilePath, configPath, outputPath, _context.BackupDirectory));
            TerminalOutputFormatter.WritePatchApply(_context.Output, result);

            return 0;
        });
    }

    private bool TryReadPatchInputs(out string assetsFilePath, out string configPath)
    {
        string? assetsPath = _prompts.ReadExistingFilePath("Assets file path");

        if (assetsPath is null)
        {
            assetsFilePath = string.Empty;
            configPath = string.Empty;
            return false;
        }

        string? manifestPath = _prompts.ReadExistingFilePath("Manifest JSON or mod zip path");

        if (manifestPath is null)
        {
            assetsFilePath = string.Empty;
            configPath = string.Empty;
            return false;
        }

        assetsFilePath = assetsPath;
        configPath = manifestPath;
        return true;
    }

    private void WritePatchPreview(string assetsFilePath, string configPath)
    {
        _context.UseService(service =>
        {
            PatchPreviewResult preview = service.PreviewPatch(new PatchPreviewRequest(assetsFilePath, configPath));
            TerminalOutputFormatter.WritePatchPreview(_context.Output, preview);

            return 0;
        });
    }

    private static Func<TerminalAssetsWorkflowServiceScope> CreateServiceScopeFactory(
        IAssetsFileService assetsFileService)
    {
        if (assetsFileService is not IAssetsReadScopeFactory readScopeFactory)
        {
            return () => new TerminalAssetsWorkflowServiceScope(new AssetsWorkflowService(assetsFileService), null);
        }

        return () =>
        {
            IAssetsReadScope readScope = readScopeFactory.CreateReadScope();
            var service = new AssetsWorkflowService(readScope, assetsFileService);

            return new TerminalAssetsWorkflowServiceScope(service, readScope);
        };
    }
}

internal sealed class TerminalAppContext
{
    private readonly Func<TerminalAssetsWorkflowServiceScope> _serviceScopeFactory;

    public TerminalAppContext(
        Func<TerminalAssetsWorkflowServiceScope> serviceScopeFactory,
        string backupDirectory,
        TextWriter output,
        TextWriter error)
    {
        _serviceScopeFactory = serviceScopeFactory;
        BackupDirectory = backupDirectory;
        Output = output;
        Error = error;
    }

    public string BackupDirectory { get; }
    public TextWriter Output { get; }
    public TextWriter Error { get; }

    public void UseService(Func<AssetsWorkflowService, int> action)
    {
        using TerminalAssetsWorkflowServiceScope scope = _serviceScopeFactory();

        action(scope.Service);
    }
}

internal sealed class TerminalAssetsWorkflowServiceScope : IDisposable
{
    private readonly IDisposable? _disposable;

    public TerminalAssetsWorkflowServiceScope(AssetsWorkflowService service, IDisposable? disposable)
    {
        Service = service;
        _disposable = disposable;
    }

    public AssetsWorkflowService Service { get; }

    public void Dispose()
    {
        _disposable?.Dispose();
    }
}
