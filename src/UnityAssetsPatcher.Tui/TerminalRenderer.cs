using System.Globalization;
using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.Tui;

public sealed class TerminalRenderer
{
    public const string ShortcutHint = "Shortcuts: ↑/↓ to choose | Esc to cancel | Ctrl + C to exit";

    private const string ApplicationTitle = "Unity Assets Patcher";
    private const int ApplicationTitleHorizontalPadding = 2;
    private const int SettingsOptionColumnWidth = 35;
    private const string ReturnToMainMenuPrompt = "Press any key to return to the main menu.";
    private const string SaveCursor = "\e[s";
    private const string RestoreCursor = "\e[u";

    private readonly IAnsiConsole _console;

    public TerminalRenderer(IAnsiConsole console)
    {
        _console = console;
    }

    public void ShowPage(
        string title,
        string? description = null,
        string shortcutHint = ShortcutHint,
        bool clear = true)
    {
        WriteApplicationHeader(shortcutHint, clear);
        _console.MarkupLine($"[bold blue]{Markup.Escape(title)}[/]");

        if (!string.IsNullOrWhiteSpace(description))
        {
            _console.MarkupLine($"[grey]{Markup.Escape(description)}[/]");
        }

        WriteBlankLine();
    }

    public void ShowReturnHint()
    {
        WriteBottomFooterHint(ReturnToMainMenuPrompt);
    }

    public void ShowShortcutHint()
    {
        WriteBottomFooterHint(ShortcutHint);
    }

    public void PrepareOutputArea()
    {
        WriteBlankLine();
        ClearBottomFooterArea();
    }

    public void WriteBlankLine()
    {
        _console.Write(new Text(Environment.NewLine));
    }

    public void WriteInfo(string message)
    {
        _console.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
    }

    public void WriteInputLabel(string label)
    {
        _console.Markup($"[blue]{Markup.Escape(label)}[/]: ");
    }

    public void WriteConfirmationLabel(string prompt)
    {
        _console.Markup($"[blue]{Markup.Escape(prompt)}[/] [grey]y/N[/]: ");
    }

    public void WriteError(string message)
    {
        _console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    }

    public void WriteChoiceList(IReadOnlyList<string> choices, int selectedIndex)
    {
        for (int i = 0; i < choices.Count; i++)
        {
            string indicator = i == selectedIndex ? ">" : " ";
            string line = $"{indicator} {choices[i]}";

            if (i == selectedIndex)
            {
                _console.MarkupLine($"[cyan]{Markup.Escape(line)}[/]");
                continue;
            }

            _console.MarkupLine(Markup.Escape(line));
        }
    }

    public void WriteAssetSummary(IReadOnlyList<AssetsInfo> assets, int totalCount)
    {
        Table table = CreateTable();
        table.AddColumn(new TableColumn("Path ID").RightAligned());
        table.AddColumn(new TableColumn("Type ID").RightAligned());
        table.AddColumn("Type Name");
        table.AddColumn(new TableColumn("Byte Size").RightAligned());

        foreach (AssetsInfo asset in assets)
        {
            table.AddRow(
                asset.PathId.ToString(CultureInfo.InvariantCulture),
                asset.TypeId.ToString(CultureInfo.InvariantCulture),
                Escape(asset.TypeName),
                asset.ByteSize.ToString(CultureInfo.InvariantCulture));
        }

        _console.Write(table);

        if (assets.Count >= totalCount)
        {
            return;
        }

        WriteBlankLine();
        WriteInfo($"Showing {assets.Count} of {totalCount} assets.");
    }

    public void WriteAssetFields(AssetsFieldInfo fieldTree)
    {
        WriteAssetField(fieldTree, 0);
    }

    public void WriteFindResults(IReadOnlyList<AssetMatch> matches)
    {
        Table table = CreateTable();
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
                Escape(match.Asset.TypeName),
                Escape(matchedFields));
        }

        _console.Write(table);
    }

    public void WriteInstallPreview(InstallPreviewResult result, TerminalSettings? settings = null)
    {
        settings ??= new TerminalSettings();

        WriteStatus("DRY RUN", "yellow");

        int assetCount = result.Files.Sum(file => file.Preview.Assets.Count);
        int operationCount = CountPreviewOperations(result);
        int changingOperationCount = CountChangingPreviewOperations(result);

        WriteSummaryRows(
            ("Mod", result.ModName),
            ("Version", result.ModVersion),
            ("Targets", result.Files.Count.ToString(CultureInfo.InvariantCulture)),
            ("Payload files", result.CopiedFiles.Count.ToString(CultureInfo.InvariantCulture)),
            ("Assets", assetCount.ToString(CultureInfo.InvariantCulture)),
            ("Operations", FormatOperationCounts(changingOperationCount, operationCount - changingOperationCount)),
            ("Elapsed", $"{FormatElapsedSeconds(result.Timing.Elapsed)} s"));

        WriteInstallPreviewTargets(result.Files);
        WriteInstallPreviewPayloads(result.CopiedFiles);

        if (settings.VerboseLogging)
        {
            WriteInstallPreviewDetails(result.Files);
        }

        if (settings.InstallTimingDetails)
        {
            WriteInstallTiming(result.Timing);
        }
    }

    public void WriteInstallResult(InstallModResult result, TerminalSettings? settings = null)
    {
        settings ??= new TerminalSettings();

        WriteStatus("INSTALLED", "green");
        WriteSummaryRows(
            ("Mod", result.ModName),
            ("Version", result.ModVersion),
            ("Patched files", result.Files.Count.ToString(CultureInfo.InvariantCulture)),
            ("Copied files", result.CopiedFiles.Count.ToString(CultureInfo.InvariantCulture)),
            ("Assets", result.Files.Sum(file => file.AssetCount).ToString(CultureInfo.InvariantCulture)),
            ("Operations", result.Files.Sum(file => file.OperationCount).ToString(CultureInfo.InvariantCulture)),
            ("Elapsed", $"{FormatElapsedSeconds(result.Timing.Elapsed)} s"));

        WriteInstallResultTargets(result.Files);
        WriteInstallResultPayloads(result.CopiedFiles);

        if (settings.InstallTimingDetails)
        {
            WriteInstallTiming(result.Timing);
        }
    }

    internal void WriteMainMenu(IReadOnlyList<ITerminalPage> pages, int selectedIndex)
    {
        const int labelColumnWidth = 18;

        for (int i = 0; i < pages.Count; i++)
        {
            ITerminalPage page = pages[i];
            string indicator = i == selectedIndex ? ">" : " ";
            string label = page.Title.PadRight(labelColumnWidth);

            _console.MarkupLine(i == selectedIndex
                ? $"[cyan]{Escape($"{indicator} {label}")}[/] [cyan]{Escape(page.Description)}[/]"
                : $"{Escape($"{indicator} {label}")} [grey]{Escape(page.Description)}[/]");

            if (i < pages.Count - 1)
            {
                WriteBlankLine();
            }
        }
    }

    internal void WriteSettings(IReadOnlyList<TerminalSettingDisplay> settings, int selectedIndex)
    {
        for (int i = 0; i < settings.Count; i++)
        {
            TerminalSettingDisplay setting = settings[i];
            string indicator = i == selectedIndex ? ">" : " ";
            string checkbox = setting.Enabled ? "[x]" : "[ ]";
            string option = $"{indicator} {checkbox} {setting.Name}".PadRight(SettingsOptionColumnWidth);

            _console.MarkupLine(i == selectedIndex
                ? $"[cyan]{Escape(option)}[/] [cyan]{Escape(setting.Description)}[/]"
                : $"{Escape(option)} [grey]{Escape(setting.Description)}[/]");
        }
    }

    public void WriteBottomFooterHint(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        int height = _console.Profile.Height;

        if (height <= 0)
        {
            WriteFooterHint(message);
            return;
        }

        _console.Cursor.SetPosition(1, height);
        _console.Markup($"[grey]{Escape(FitToWidth(message, _console.Profile.Width))}[/]");
        ClearBottomFooterArea(clearFooterLine: false, preserveCursor: false);
    }

    public void ClearBottomFooterArea()
    {
        ClearBottomFooterArea(clearFooterLine: true, preserveCursor: true);
    }

    private void WriteFooterHint(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        WriteBlankLine();
        WriteInfo(message);
    }

    private void ClearBottomFooterArea(bool clearFooterLine, bool preserveCursor)
    {
        int height = _console.Profile.Height;

        if (height <= 1)
        {
            return;
        }

        string clearLine = new(' ', Math.Max(_console.Profile.Width, 0));
        int contentLine = Math.Max(height - 2, 1);

        if (preserveCursor)
        {
            _console.Write(new Text(SaveCursor));
        }

        if (clearFooterLine)
        {
            _console.Cursor.SetPosition(1, height);
            _console.Write(new Text(clearLine));
        }

        _console.Cursor.SetPosition(1, height - 1);
        _console.Write(new Text(clearLine));
        _console.Cursor.SetPosition(1, contentLine);
        _console.Write(new Text(clearLine));
        _console.Cursor.SetPosition(1, contentLine);

        if (preserveCursor)
        {
            _console.Write(new Text(RestoreCursor));
        }
    }

    private void WriteApplicationHeader(string? footerHint = null, bool clear = true)
    {
        if (clear)
        {
            _console.Clear(home: true);
        }
        else
        {
            _console.Cursor.SetPosition(1, 1);
        }

        WriteBottomFooterHint(footerHint);
        _console.Cursor.SetPosition(1, 1);
        WriteApplicationTitle();
        WriteBlankLine();
    }

    private void WriteApplicationTitle()
    {
        WriteApplicationTitle(BuildInfo.DisplayVersion);
    }

    private void WriteApplicationTitle(string displayVersion)
    {
        string title = $"{ApplicationTitle} ({displayVersion})";
        int boxWidth = title.Length + (ApplicationTitleHorizontalPadding * 2) + 2;

        WriteTitleBoxLine("╭", "─", "╮", boxWidth);
        WriteTitleContentLine(title, greyStartIndex: ApplicationTitle.Length + 1);
        WriteTitleBoxLine("╰", "─", "╯", boxWidth);
    }

    private void WriteInstallPreviewTargets(IReadOnlyList<InstallPreviewFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        WriteBlankLine();
        _console.MarkupLine("[blue]Targets[/]");

        foreach (InstallPreviewFileResult file in files)
        {
            int assetCount = file.Preview.Assets.Count;
            int operationCount = file.Preview.Assets.Sum(asset => asset.Operations.Count);
            int changingOperationCount = file.Preview.Assets.Sum(asset =>
                asset.Operations.Count(operation => operation.WillChange));

            _console.MarkupLine(
                $"- {Escape(file.Target)}: {FormatCount(assetCount, "asset")}, {FormatOperationCounts(changingOperationCount, operationCount - changingOperationCount)}");
            _console.MarkupLine($"  [grey]{Escape(file.AssetsFilePath)}[/]");
        }
    }

    private void WriteInstallPreviewPayloads(IReadOnlyList<InstallCopyFilePreviewResult> copiedFiles)
    {
        if (copiedFiles.Count == 0)
        {
            return;
        }

        WriteBlankLine();
        _console.MarkupLine("[blue]Payload files[/]");

        foreach (InstallCopyFilePreviewResult copiedFile in copiedFiles)
        {
            string status = copiedFile.WillCopy ? "will copy" : "skipped, destination exists";
            _console.MarkupLine(
                $"- {Escape(Path.GetFileName(copiedFile.Source))}: {Escape(status)}");
        }
    }

    private void WriteInstallPreviewDetails(IReadOnlyList<InstallPreviewFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        WriteBlankLine();
        _console.MarkupLine("[blue]Details[/]");

        foreach (InstallPreviewFileResult file in files)
        {
            WriteBlankLine();
            _console.MarkupLine($"[blue]Target[/] {Escape(file.Target)}");
            WritePatchPreviewAssets(file.Preview);
        }
    }

    private void WriteInstallResultTargets(IReadOnlyList<InstallModFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        WriteBlankLine();
        _console.MarkupLine("[blue]Patched files[/]");

        foreach (InstallModFileResult file in files)
        {
            _console.MarkupLine(
                $"- {Escape(file.Target)}: {FormatCount(file.AssetCount, "asset")}, {FormatCount(file.OperationCount, "operation")}");
            _console.MarkupLine($"  [grey]Backup[/] {Escape(file.BackupPath)}");
        }
    }

    private void WriteInstallResultPayloads(IReadOnlyList<InstallCopiedFileResult> copiedFiles)
    {
        if (copiedFiles.Count == 0)
        {
            return;
        }

        WriteBlankLine();
        _console.MarkupLine("[blue]Copied files[/]");

        foreach (InstallCopiedFileResult copiedFile in copiedFiles)
        {
            _console.MarkupLine($"- {Escape(Path.GetFileName(copiedFile.DestinationPath))}");
        }
    }

    private void WritePatchPreviewAssets(PatchPreviewResult preview)
    {
        foreach (PatchPreviewAssetResult assetResult in preview.Assets)
        {
            WriteBlankLine();
            _console.MarkupLine(
                $"[grey]Path ID {assetResult.Asset.PathId.ToString(CultureInfo.InvariantCulture)} ({Escape(assetResult.Asset.TypeName)})[/]");

            foreach (PatchPreviewOperationResult operation in assetResult.Operations)
            {
                if (!operation.WillChange)
                {
                    _console.MarkupLine(
                        $"  {Escape(operation.Path)}: skipped, current value {Escape(operation.OldValue)} does not match expected {Escape(JsonUtils.FormatElementValue(operation.From))}");
                    continue;
                }

                _console.MarkupLine(
                    $"  {Escape(operation.Path)}: {Escape(operation.OldValue)} [grey]->[/] {Escape(JsonUtils.FormatElementValue(operation.To))}");
            }
        }
    }

    private static Table CreateTable()
    {
        return new Table()
            .Border(TableBorder.Ascii)
            .BorderColor(Color.Grey);
    }

    private void WriteAssetField(AssetsFieldInfo field, int depth)
    {
        string indentation = new(' ', depth * 2);
        string value = field.Value is null ? string.Empty : $": {field.Value}";
        _console.MarkupLine($"{indentation}{Escape(field.Name)} ({Escape(field.TypeName)}){Escape(value)}");

        foreach (AssetsFieldInfo child in field.Children)
        {
            WriteAssetField(child, depth + 1);
        }
    }

    private void WriteStatus(string label, string color)
    {
        _console.MarkupLine($"[bold {color}]{Markup.Escape(label)}[/]");
    }

    private void WriteSummaryRows(params (string Label, string Value)[] rows)
    {
        foreach ((string label, string value) in rows)
        {
            _console.MarkupLine($"[grey]{Escape(label),-14}[/] {Escape(value)}");
        }
    }

    private static int CountPreviewOperations(InstallPreviewResult result)
    {
        return result.Files.Sum(file => file.Preview.Assets.Sum(asset => asset.Operations.Count));
    }

    private static int CountChangingPreviewOperations(InstallPreviewResult result)
    {
        return result.Files.Sum(file => file.Preview.Assets.Sum(asset =>
            asset.Operations.Count(operation => operation.WillChange)));
    }

    private static string FormatOperationCounts(int changingCount, int skippedCount)
    {
        string changing = FormatCount(changingCount, "operation");

        return skippedCount == 0
            ? changing
            : $"{changing} changing, {FormatCount(skippedCount, "operation")} skipped";
    }

    private static string FormatCount(int count, string singular)
    {
        string noun = count == 1 ? singular : $"{singular}s";

        return $"{count.ToString(CultureInfo.InvariantCulture)} {noun}";
    }

    private static string FormatElapsedSeconds(TimeSpan elapsed)
    {
        return elapsed.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private void WriteInstallTiming(InstallTimingResult timing)
    {
        WriteBlankLine();
        _console.MarkupLine("[blue]Timing[/]");
        WriteSummaryRows(
            ("Read package", $"{FormatElapsedSeconds(timing.ReadPackage)} s"),
            ("Prepare sources", $"{FormatElapsedSeconds(timing.PrepareSources)} s"),
            ("Find game files", $"{FormatElapsedSeconds(timing.FindGameFiles)} s"),
            ("Analyze changes", $"{FormatElapsedSeconds(timing.AnalyzeChanges)} s"),
            ("Apply patches", FormatTimingStage(timing.ApplyPatches)),
            ("Copy files", FormatTimingStage(timing.CopyFiles)));
    }

    private static string FormatTimingStage(TimeSpan? elapsed)
    {
        return elapsed is null ? "skipped" : $"{FormatElapsedSeconds(elapsed.Value)} s";
    }

    private static string Escape(string value)
    {
        return Markup.Escape(value);
    }

    private static string FitToWidth(string value, int width)
    {
        if (width <= 0)
        {
            return value;
        }

        return value.Length <= width
            ? value.PadRight(width)
            : value[..width];
    }

    private void WriteTitleBoxLine(
        string left,
        string horizontal,
        string right,
        int width)
    {
        _console.MarkupLine(
            $"[grey]{Escape(left + string.Concat(Enumerable.Repeat(horizontal, width - 2)) + right)}[/]");
    }

    private void WriteTitleContentLine(
        string content,
        int? greyStartIndex = null,
        int? boldStartIndex = null)
    {
        string paddedContent =
            $"{new string(' ', ApplicationTitleHorizontalPadding)}{content}{new string(' ', ApplicationTitleHorizontalPadding)}";

        _console.Markup("[grey]│[/]");

        if (greyStartIndex is null)
        {
            _console.Markup(Escape(paddedContent));
        }
        else
        {
            int greyIndex = Math.Clamp(greyStartIndex.Value + ApplicationTitleHorizontalPadding, 0,
                paddedContent.Length);
            string normal = paddedContent[..greyIndex];
            string grey = paddedContent[greyIndex..];

            _console.Markup(Escape(normal));

            if (boldStartIndex is null)
            {
                _console.Markup($"[grey]{Escape(grey)}[/]");
            }
            else
            {
                int boldIndex = Math.Clamp(boldStartIndex.Value, greyIndex, paddedContent.Length);
                string label = paddedContent[greyIndex..boldIndex];
                string value = paddedContent[boldIndex..];

                _console.Markup($"[grey]{Escape(label)}[/]");
                _console.Markup($"[bold]{Escape(value)}[/]");
            }
        }

        _console.MarkupLine("[grey]│[/]");
    }
}
