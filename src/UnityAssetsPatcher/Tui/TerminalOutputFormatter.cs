using System.Globalization;
using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Core.Json;

namespace UnityAssetsPatcher.Tui;

public static class TerminalOutputFormatter
{
    private const string ApplicationTitle = "Unity Assets Patcher";
    private const int ApplicationTitleHorizontalPadding = 2;
    private const int SettingsOptionColumnWidth = 34;
    private const string SaveCursor = "\e[s";
    private const string RestoreCursor = "\e[u";

    public static void WritePageHeader(
        IAnsiConsole console,
        string title,
        string? subtitle = null,
        string? footerHint = null,
        bool clear = true)
    {
        WriteApplicationHeader(console, footerHint, clear);
        console.MarkupLine($"[bold blue]{Markup.Escape(title)}[/]");

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            console.MarkupLine($"[grey]{Markup.Escape(subtitle)}[/]");
        }

        WriteBlankLine(console);
    }

    public static void WriteBlankLine(IAnsiConsole console)
    {
        console.Write(new Text(Environment.NewLine));
    }

    public static void WriteInfo(IAnsiConsole console, string message)
    {
        console.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
    }

    private static void WriteFooterHint(IAnsiConsole console, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        WriteBlankLine(console);
        WriteInfo(console, message);
    }

    public static void WriteBottomFooterHint(IAnsiConsole console, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        int height = console.Profile.Height;

        if (height <= 0)
        {
            WriteFooterHint(console, message);
            return;
        }

        console.Cursor.SetPosition(1, height);
        console.Markup($"[grey]{Escape(FitToWidth(message, console.Profile.Width))}[/]");
        ClearBottomFooterArea(console, clearFooterLine: false, preserveCursor: false);
    }

    public static void ClearBottomFooterArea(IAnsiConsole console)
    {
        ClearBottomFooterArea(console, clearFooterLine: true, preserveCursor: true);
    }

    private static void ClearBottomFooterArea(IAnsiConsole console, bool clearFooterLine, bool preserveCursor)
    {
        int height = console.Profile.Height;

        if (height <= 1)
        {
            return;
        }

        string clearLine = new(' ', Math.Max(console.Profile.Width, 0));
        int contentLine = Math.Max(height - 2, 1);

        if (preserveCursor)
        {
            console.Write(new Text(SaveCursor));
        }

        if (clearFooterLine)
        {
            console.Cursor.SetPosition(1, height);
            console.Write(new Text(clearLine));
        }

        console.Cursor.SetPosition(1, height - 1);
        console.Write(new Text(clearLine));
        console.Cursor.SetPosition(1, contentLine);
        console.Write(new Text(clearLine));
        console.Cursor.SetPosition(1, contentLine);

        if (preserveCursor)
        {
            console.Write(new Text(RestoreCursor));
        }
    }

    public static void WriteError(IAnsiConsole console, string message)
    {
        console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    }

    public static void WriteAssetSummary(IAnsiConsole console, IReadOnlyList<AssetsInfo> assets, int? limit)
    {
        Table table = CreateTable();
        table.AddColumn(new TableColumn("Path ID").RightAligned());
        table.AddColumn(new TableColumn("Type ID").RightAligned());
        table.AddColumn("Type Name");
        table.AddColumn(new TableColumn("Byte Size").RightAligned());

        var assetsToPrint = limit is null ? assets : assets.Take(limit.Value);

        foreach (AssetsInfo asset in assetsToPrint)
        {
            table.AddRow(
                asset.PathId.ToString(CultureInfo.InvariantCulture),
                asset.TypeId.ToString(CultureInfo.InvariantCulture),
                Escape(asset.TypeName),
                asset.ByteSize.ToString(CultureInfo.InvariantCulture));
        }

        console.Write(table);

        if (limit is null || assets.Count <= limit.Value)
        {
            return;
        }

        WriteBlankLine(console);
        WriteInfo(console, $"Showing {limit.Value} of {assets.Count} assets.");
    }

    public static void WriteAssetFields(IAnsiConsole console, AssetsFieldInfo fieldTree)
    {
        WriteAssetField(console, fieldTree, 0);
    }

    public static void WriteFindResults(IAnsiConsole console, IReadOnlyList<AssetMatch> matches)
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

        console.Write(table);
    }

    public static void WriteInstallPreview(
        IAnsiConsole console,
        InstallPreviewResult result,
        TerminalSettings? settings = null)
    {
        settings ??= new TerminalSettings();

        WriteStatus(console, "DRY RUN", "yellow");

        int assetCount = result.Files.Sum(file => file.Preview.Assets.Count);
        int operationCount = CountPreviewOperations(result);
        int changingOperationCount = CountChangingPreviewOperations(result);

        WriteSummaryRows(
            console,
            ("Mod", result.ModName),
            ("Version", result.ModVersion),
            ("Targets", result.Files.Count.ToString(CultureInfo.InvariantCulture)),
            ("Payload files", result.CopiedFiles.Count.ToString(CultureInfo.InvariantCulture)),
            ("Assets", assetCount.ToString(CultureInfo.InvariantCulture)),
            ("Operations", FormatOperationCounts(changingOperationCount, operationCount - changingOperationCount)),
            ("Elapsed", $"{FormatElapsedSeconds(result.Timing.Elapsed)} s"));

        WriteInstallPreviewTargets(console, result.Files);
        WriteInstallPreviewPayloads(console, result.CopiedFiles);

        if (settings.VerboseLogging)
        {
            WriteInstallPreviewDetails(console, result.Files);
        }

        if (settings.InstallTimingDetails)
        {
            WriteInstallTiming(console, result.Timing);
        }
    }

    public static void WriteInstallResult(
        IAnsiConsole console,
        InstallModResult result,
        TerminalSettings? settings = null)
    {
        settings ??= new TerminalSettings();

        WriteStatus(console, "INSTALLED", "green");
        WriteSummaryRows(
            console,
            ("Mod", result.ModName),
            ("Version", result.ModVersion),
            ("Patched files", result.Files.Count.ToString(CultureInfo.InvariantCulture)),
            ("Copied files", result.CopiedFiles.Count.ToString(CultureInfo.InvariantCulture)),
            ("Assets", result.Files.Sum(file => file.AssetCount).ToString(CultureInfo.InvariantCulture)),
            ("Operations", result.Files.Sum(file => file.OperationCount).ToString(CultureInfo.InvariantCulture)),
            ("Elapsed", $"{FormatElapsedSeconds(result.Timing.Elapsed)} s"));

        WriteInstallResultTargets(console, result.Files);
        WriteInstallResultPayloads(console, result.CopiedFiles);

        if (settings.InstallTimingDetails)
        {
            WriteInstallTiming(console, result.Timing);
        }
    }

    internal static void WriteMainMenu(
        IAnsiConsole console,
        IReadOnlyList<TerminalPage> pages,
        int selectedIndex)
    {
        const int labelColumnWidth = 18;

        for (int i = 0; i < pages.Count; i++)
        {
            TerminalPage page = pages[i];
            string indicator = i == selectedIndex ? ">" : " ";
            string label = page.Title.PadRight(labelColumnWidth);

            console.MarkupLine(i == selectedIndex
                ? $"[cyan]{Escape($"{indicator} {label}")}[/] [cyan]{Escape(page.Description)}[/]"
                : $"{Escape($"{indicator} {label}")} [grey]{Escape(page.Description)}[/]");

            if (i < pages.Count - 1)
            {
                WriteBlankLine(console);
            }
        }
    }

    internal static void WriteSettings(
        IAnsiConsole console,
        IReadOnlyList<TerminalSettingDisplay> settings,
        int selectedIndex)
    {
        for (int i = 0; i < settings.Count; i++)
        {
            TerminalSettingDisplay setting = settings[i];
            string indicator = i == selectedIndex ? ">" : " ";
            string checkbox = setting.Enabled ? "[x]" : "[ ]";
            string option = $"{indicator} {checkbox} {setting.Name}".PadRight(SettingsOptionColumnWidth);

            console.MarkupLine(i == selectedIndex
                ? $"[cyan]{Escape(option)}[/] [cyan]{Escape(setting.Description)}[/]"
                : $"{Escape(option)} [grey]{Escape(setting.Description)}[/]");
        }
    }

    private static void WriteApplicationHeader(IAnsiConsole console, string? footerHint = null, bool clear = true)
    {
        if (clear)
        {
            console.Clear(home: true);
        }
        else
        {
            console.Cursor.SetPosition(1, 1);
        }

        WriteBottomFooterHint(console, footerHint);
        console.Cursor.SetPosition(1, 1);
        WriteApplicationTitle(console);
        WriteBlankLine(console);
    }

    private static void WriteApplicationTitle(IAnsiConsole console)
    {
        WriteApplicationTitle(console, BuildInfo.DisplayVersion);
    }

    private static void WriteApplicationTitle(IAnsiConsole console, string displayVersion)
    {
        string title = $"{ApplicationTitle} ({displayVersion})";
        int boxWidth = title.Length + (ApplicationTitleHorizontalPadding * 2) + 2;

        WriteTitleBoxLine(console, "╭", "─", "╮", boxWidth);
        WriteTitleContentLine(console, title, greyStartIndex: ApplicationTitle.Length + 1);
        WriteTitleBoxLine(console, "╰", "─", "╯", boxWidth);
    }

    private static void WriteInstallPreviewTargets(
        IAnsiConsole console,
        IReadOnlyList<InstallPreviewFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        WriteBlankLine(console);
        console.MarkupLine("[blue]Targets[/]");

        foreach (InstallPreviewFileResult file in files)
        {
            int assetCount = file.Preview.Assets.Count;
            int operationCount = file.Preview.Assets.Sum(asset => asset.Operations.Count);
            int changingOperationCount = file.Preview.Assets.Sum(asset =>
                asset.Operations.Count(operation => operation.WillChange));

            console.MarkupLine(
                $"- {Escape(file.Target)}: {FormatCount(assetCount, "asset")}, {FormatOperationCounts(changingOperationCount, operationCount - changingOperationCount)}");
            console.MarkupLine($"  [grey]{Escape(file.AssetsFilePath)}[/]");
        }
    }

    private static void WriteInstallPreviewPayloads(
        IAnsiConsole console,
        IReadOnlyList<InstallCopyFilePreviewResult> copiedFiles)
    {
        if (copiedFiles.Count == 0)
        {
            return;
        }

        WriteBlankLine(console);
        console.MarkupLine("[blue]Payload files[/]");

        foreach (InstallCopyFilePreviewResult copiedFile in copiedFiles)
        {
            string status = copiedFile.WillCopy ? "will copy" : "skipped, destination exists";
            console.MarkupLine(
                $"- {Escape(Path.GetFileName(copiedFile.Source))}: {Escape(status)}");
        }
    }

    private static void WriteInstallPreviewDetails(
        IAnsiConsole console,
        IReadOnlyList<InstallPreviewFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        WriteBlankLine(console);
        console.MarkupLine("[blue]Details[/]");

        foreach (InstallPreviewFileResult file in files)
        {
            WriteBlankLine(console);
            console.MarkupLine($"[blue]Target[/] {Escape(file.Target)}");
            WritePatchPreviewAssets(console, file.Preview);
        }
    }

    private static void WriteInstallResultTargets(
        IAnsiConsole console,
        IReadOnlyList<InstallModFileResult> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        WriteBlankLine(console);
        console.MarkupLine("[blue]Patched files[/]");

        foreach (InstallModFileResult file in files)
        {
            console.MarkupLine(
                $"- {Escape(file.Target)}: {FormatCount(file.AssetCount, "asset")}, {FormatCount(file.OperationCount, "operation")}");
            console.MarkupLine($"  [grey]Backup[/] {Escape(file.BackupPath)}");
        }
    }

    private static void WriteInstallResultPayloads(
        IAnsiConsole console,
        IReadOnlyList<InstallCopiedFileResult> copiedFiles)
    {
        if (copiedFiles.Count == 0)
        {
            return;
        }

        WriteBlankLine(console);
        console.MarkupLine("[blue]Copied files[/]");

        foreach (InstallCopiedFileResult copiedFile in copiedFiles)
        {
            console.MarkupLine($"- {Escape(Path.GetFileName(copiedFile.DestinationPath))}");
        }
    }

    private static void WritePatchPreviewAssets(IAnsiConsole console, PatchPreviewResult preview)
    {
        foreach (PatchPreviewAssetResult assetResult in preview.Assets)
        {
            WriteBlankLine(console);
            console.MarkupLine(
                $"[grey]Path ID {assetResult.Asset.PathId.ToString(CultureInfo.InvariantCulture)} ({Escape(assetResult.Asset.TypeName)})[/]");

            foreach (PatchPreviewOperationResult operation in assetResult.Operations)
            {
                if (!operation.WillChange)
                {
                    console.MarkupLine(
                        $"  {Escape(operation.Path)}: skipped, current value {Escape(operation.OldValue)} does not match expected {Escape(JsonUtils.FormatElementValue(operation.From))}");
                    continue;
                }

                console.MarkupLine(
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

    private static void WriteAssetField(IAnsiConsole console, AssetsFieldInfo field, int depth)
    {
        string indentation = new(' ', depth * 2);
        string value = field.Value is null ? string.Empty : $": {field.Value}";
        console.MarkupLine($"{indentation}{Escape(field.Name)} ({Escape(field.TypeName)}){Escape(value)}");

        foreach (AssetsFieldInfo child in field.Children)
        {
            WriteAssetField(console, child, depth + 1);
        }
    }

    private static void WriteStatus(IAnsiConsole console, string label, string color)
    {
        console.MarkupLine($"[bold {color}]{Markup.Escape(label)}[/]");
    }

    private static void WriteSummaryRows(IAnsiConsole console, params (string Label, string Value)[] rows)
    {
        foreach ((string label, string value) in rows)
        {
            console.MarkupLine($"[grey]{Escape(label),-14}[/] {Escape(value)}");
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

    private static void WriteInstallTiming(IAnsiConsole console, InstallTimingResult timing)
    {
        WriteBlankLine(console);
        console.MarkupLine("[blue]Timing[/]");
        WriteSummaryRows(
            console,
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

    private static void WriteTitleBoxLine(
        IAnsiConsole console,
        string left,
        string horizontal,
        string right,
        int width)
    {
        console.MarkupLine(
            $"[grey]{Escape(left + string.Concat(Enumerable.Repeat(horizontal, width - 2)) + right)}[/]");
    }

    private static void WriteTitleContentLine(
        IAnsiConsole console,
        string content,
        int? greyStartIndex = null,
        int? boldStartIndex = null)
    {
        string paddedContent =
            $"{new string(' ', ApplicationTitleHorizontalPadding)}{content}{new string(' ', ApplicationTitleHorizontalPadding)}";

        console.Markup("[grey]│[/]");

        if (greyStartIndex is null)
        {
            console.Markup(Escape(paddedContent));
        }
        else
        {
            int greyIndex = Math.Clamp(greyStartIndex.Value + ApplicationTitleHorizontalPadding, 0,
                paddedContent.Length);
            string normal = paddedContent[..greyIndex];
            string grey = paddedContent[greyIndex..];

            console.Markup(Escape(normal));

            if (boldStartIndex is null)
            {
                console.Markup($"[grey]{Escape(grey)}[/]");
            }
            else
            {
                int boldIndex = Math.Clamp(boldStartIndex.Value, greyIndex, paddedContent.Length);
                string label = paddedContent[greyIndex..boldIndex];
                string value = paddedContent[boldIndex..];

                console.Markup($"[grey]{Escape(label)}[/]");
                console.Markup($"[bold]{Escape(value)}[/]");
            }
        }

        console.MarkupLine("[grey]│[/]");
    }
}
