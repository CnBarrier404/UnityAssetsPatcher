using System.Globalization;
using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class InstallModView : View, ITerminalContentView, ITerminalRenderRequester
{
    public event EventHandler? RenderRequested;

    public string ShortcutHint => LocalizedStrings.InstallPage_ShortcutHint;

    private readonly IWorkflowService _workflowService;
    private readonly TerminalSettings _settings;
    private readonly Action _returnToMainMenu;
    private readonly TextField _modPath;
    private readonly Label _message;
    private readonly View _form;
    private TextField? _gameDirectory;
    private View? _optionalGroupArea;
    private readonly List<OptionalGroupChoice> _optionalGroups = [];
    private bool _isAnalyzing;

    public InstallModView(
        IWorkflowService workflowService,
        TerminalSettings settings,
        Action returnToMainMenu)
    {
        _workflowService = workflowService;
        _settings = settings;
        _returnToMainMenu = returnToMainMenu;
        KeyDown += (_, key) =>
        {
            if (key != Key.Esc)
            {
                return;
            }

            key.Handled = true;
            _returnToMainMenu();
        };

        var heading = new Label { Text = LocalizedStrings.MainMenu_InstallMod_Title, X = 0, Y = 0 };
        heading.SetScheme(TerminalGUITheme.Title);
        var description = new Label
        {
            Text = LocalizedStrings.MainMenu_InstallMod_Description,
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
        };
        description.SetScheme(TerminalGUITheme.Muted);

        _form = new View
        {
            X = 0,
            Y = 3,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
        };
        string pathPrompt = $"{LocalizedStrings.InstallPage_ModZipPathPrompt}: ";
        var pathLabel = new Label { Text = pathPrompt, X = 0, Y = 0 };
        pathLabel.SetScheme(TerminalGUITheme.Label);
        _modPath = new TextField
        {
            X = GetDisplayWidth(pathPrompt),
            Y = 0,
            Width = Dim.Fill(),
        };
        _modPath.SetScheme(CreateInputScheme());
        _modPath.Accepted += (_, _) => Preview();
        _message = new Label
        {
            X = 0,
            Y = Pos.Bottom(_modPath) + 2,
            Width = Dim.Fill(),
            Visible = false,
        };
        _form.Add(pathLabel, _modPath, _message);
        Add(heading, description, _form);
    }

    private void Preview()
    {
        if (_isAnalyzing)
        {
            return;
        }

        ClearMessage();
        string modPath = TerminalPathNormalizer.Normalize(_modPath.Text);
        if (string.IsNullOrWhiteSpace(modPath))
        {
            ShowInputError(
                _modPath,
                string.Format(LocalizedStrings.Prompt_LabelRequiredFormat,
                    LocalizedStrings.InstallPage_ModZipPathPrompt));

            return;
        }

        if (!File.Exists(modPath))
        {
            ShowInputError(_modPath, string.Format(LocalizedStrings.Prompt_FileNotFoundFormat, modPath));

            return;
        }

        _modPath.Text = modPath;

        string? gameDirectory = _gameDirectory is null
            ? null
            : TerminalPathNormalizer.Normalize(_gameDirectory.Text);

        if (!string.IsNullOrEmpty(gameDirectory) && !Directory.Exists(gameDirectory))
        {
            ShowInputError(
                _gameDirectory!,
                string.Format(LocalizedStrings.Prompt_DirectoryNotFoundFormat, gameDirectory));

            return;
        }

        if (_gameDirectory is not null && gameDirectory is not null)
        {
            _gameDirectory.Text = gameDirectory;
        }

        IReadOnlyList<string> selectedGroups = _optionalGroups
            .Where(group => group.IsSelected)
            .Select(group => group.Name)
            .ToArray();

        _isAnalyzing = true;

        try
        {
            ShowInfo(LocalizedStrings.InstallPage_AnalyzingMod);
            RenderRequested?.Invoke(this, EventArgs.Empty);
            InstallPreviewResult result = _workflowService.PreviewInstall(
                new InstallRequest(modPath, gameDirectory)
                {
                    SelectedOptionalGroups = selectedGroups,
                });

            if (result.OptionalGroups.Count > 0 && _optionalGroupArea is null)
            {
                ShowOptionalGroups(result.OptionalGroups);
                ClearMessage();

                return;
            }

            ShowPreview(result, modPath, gameDirectory, selectedGroups);
        }
        catch (DirectoryNotFoundException exception) when (string.IsNullOrEmpty(gameDirectory))
        {
            ShowGameDirectory(exception.Message);
        }
        catch (Exception exception)
        {
            ShowInputError(_modPath, exception.Message);
        }
        finally
        {
            _isAnalyzing = false;
        }
    }

    private void ShowGameDirectory(string message)
    {
        if (_gameDirectory is null)
        {
            string prompt = $"{LocalizedStrings.InstallPage_GameDirectoryPrompt}: ";
            Pos gameDirectoryRow = Pos.Bottom(_modPath) + 2;
            var label = new Label { Text = prompt, X = 0, Y = gameDirectoryRow };
            label.SetScheme(TerminalGUITheme.Label);
            _gameDirectory = new TextField
            {
                X = GetDisplayWidth(prompt),
                Y = gameDirectoryRow,
                Width = Dim.Fill(),
            };
            _gameDirectory.SetScheme(CreateInputScheme());
            _gameDirectory.Accepted += (_, _) => Preview();
            _message.Y = Pos.Bottom(_gameDirectory) + 1;
            _form.Add(label, _gameDirectory);
            _gameDirectory.SetFocus();
        }

        ShowInfo(message);
    }

    private void ShowOptionalGroups(IReadOnlyList<(string Name, string? Description)> groups)
    {
        Pos optionalGroupsRow = _gameDirectory is null
            ? Pos.Bottom(_modPath) + 2
            : Pos.Bottom(_gameDirectory) + 2;
        _optionalGroupArea = new View
        {
            X = 0,
            Y = optionalGroupsRow,
            Width = Dim.Fill(),
            Height = (groups.Count * 2) + 4,
            CanFocus = true,
        };
        var heading = new Label { Text = LocalizedStrings.InstallPage_OptionalGroupsHeader, X = 0, Y = 0 };
        heading.SetScheme(TerminalGUITheme.Preview);
        _optionalGroupArea.Add(heading);

        for (int index = 0; index < groups.Count; index++)
        {
            (string name, string? description) = groups[index];
            int choiceRow = 2 + (index * 2);
            var choice = new OptionalGroupChoice(name, description, choiceRow);
            _optionalGroups.Add(choice);
            _optionalGroupArea.Add(choice.Button, choice.Description);
        }

        int submitRow = 3 + (groups.Count * 2);
        Button submit = CreatePrimaryActionButton(
            LocalizedStrings.InstallPage_SubmitAction,
            0,
            submitRow);
        submit.Accepted += (_, _) => Preview();
        _optionalGroupArea.Add(submit);

        _message.Y = Pos.Bottom(_optionalGroupArea) + 1;
        _form.Add(_optionalGroupArea);
        _optionalGroups[0].Button.SetFocus();
    }

    private void ShowPreview(
        InstallPreviewResult result,
        string modPath,
        string? gameDirectory,
        IReadOnlyList<string> selectedGroups)
    {
        _form.RemoveAll();
        var summaryRows = GetPreviewSummaryRows(result);
        var status = new Label { Text = LocalizedStrings.InstallPreview_DryRunStatus, X = 0, Y = 0 };
        status.SetScheme(TerminalGUITheme.Preview);
        TableView summary = CreateSummaryTable(summaryRows, 2);
        _form.Add(status, summary);

        int nextRow = summaryRows.Length + 3;
        var patches = GetChanges(result.Changes, InstallChangeKind.Patch);
        if (patches.Length > 0)
        {
            nextRow = AddPreviewTargets(patches, nextRow);
        }

        string verboseText = FormatPreviewVerboseDetails(result, _settings.VerboseOutput);
        if (!string.IsNullOrEmpty(verboseText))
        {
            int detailsHeight = GetReportHeight(verboseText);
#pragma warning disable CS0618 // Terminal.Gui has no bundled read-only, scrollable replacement yet.
            var output = new TextView
            {
                X = 0,
                Y = nextRow,
                Width = Dim.Fill(),
                Height = detailsHeight,
                ReadOnly = true,
                Text = verboseText,
            };
#pragma warning restore CS0618
            output.SetScheme(TerminalGUITheme.Base);
            _form.Add(output);
            nextRow += detailsHeight + 1;
        }

        int actionRow = nextRow + 1;
        Button install = CreateActionButton(LocalizedStrings.InstallPage_InstallAction, 0, actionRow);
        install.Accepted += (_, _) => Install(modPath, gameDirectory, selectedGroups);
        Button back = CreateActionButton(LocalizedStrings.InstallPage_BackAction, 0, actionRow + 2);
        back.Accepted += (_, _) => _returnToMainMenu();
        _form.Add(install, back);
        install.SetFocus();
    }

    private void Install(string modPath, string? gameDirectory, IReadOnlyList<string> selectedGroups)
    {
        try
        {
            InstallModResult result = _workflowService.Install(
                new InstallRequest(modPath, gameDirectory)
                {
                    SelectedOptionalGroups = selectedGroups,
                });
            ShowResult(result);
        }
        catch (Exception exception)
        {
            ShowResult(exception.Message, isError: true);
        }
    }

    private void ShowResult(InstallModResult result)
    {
        ShowResult(result, _settings.VerboseOutput);
    }

    private void ShowResult(InstallModResult result, bool verbose)
    {
        _form.RemoveAll();
        var summaryRows = GetResultSummaryRows(result);
        string text = FormatResultDetails(result, verbose);
        int detailsHeight = string.IsNullOrEmpty(text) ? 0 : GetReportHeight(text);
        var status = new Label { Text = LocalizedStrings.InstallResult_InstalledStatus, X = 0, Y = 0 };
        status.SetScheme(TerminalGUITheme.Success);
        TableView summary = CreateSummaryTable(summaryRows, 2);
        int detailsRow = summaryRows.Length + 3;
#pragma warning disable CS0618 // Terminal.Gui has no bundled read-only, scrollable replacement yet.
        var output = new TextView
        {
            X = 0,
            Y = detailsRow,
            Width = Dim.Fill(),
            Height = detailsHeight,
            ReadOnly = true,
            Text = text,
        };
#pragma warning restore CS0618
        output.SetScheme(TerminalGUITheme.Base);
        int actionRow = detailsRow + detailsHeight + 1;
        Button back = CreateActionButton(LocalizedStrings.InstallPage_ReturnAction, 0, actionRow);
        back.Accepted += (_, _) => _returnToMainMenu();
        _form.Add(status, summary, output, back);
        back.SetFocus();
    }

    private void ShowResult(string text, bool isError)
    {
        _form.RemoveAll();
        int outputHeight = GetReportHeight(text);
#pragma warning disable CS0618 // Terminal.Gui has no bundled read-only, scrollable replacement yet.
        var output = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = outputHeight,
            ReadOnly = true,
            Text = text,
        };
#pragma warning restore CS0618
        output.SetScheme(isError ? TerminalGUITheme.Error : TerminalGUITheme.Base);
        Button back = CreateActionButton(LocalizedStrings.InstallPage_ReturnAction, 0, outputHeight + 1);
        back.Accepted += (_, _) => _returnToMainMenu();
        _form.Add(output, back);
        back.SetFocus();
    }

    private void ShowInfo(string message)
    {
        _message.Visible = true;
        _message.Text = message;
        _message.SetScheme(TerminalGUITheme.Muted);
    }

    private void ShowError(string message)
    {
        _message.Visible = true;
        _message.Text = message;
        _message.SetScheme(TerminalGUITheme.Error);
    }

    private void ShowInputError(TextField input, string message)
    {
        input.Text = string.Empty;
        input.SetFocus();
        ShowError(message);
    }

    private void ClearMessage()
    {
        _message.Text = string.Empty;
        _message.Visible = false;
    }

    private static Button CreateActionButton(string text, Pos x, Pos y)
    {
        string normalText = $"  {text}";
        string focusedText = $"> {text}";
        var button = new Button
        {
            Text = normalText,
            X = x,
            Y = y,
            NoDecorations = true,
            NoPadding = true,
            ShadowStyle = ShadowStyles.None,
        };
        button.SetScheme(CreateChoiceScheme());
        button.HasFocusChanged += (_, _) => button.Text = button.HasFocus ? focusedText : normalText;
        return button;
    }

    private static Button CreatePrimaryActionButton(string text, Pos x, Pos y)
    {
        string normalText = $"  {text}";
        string focusedText = $"> {text}";
        var button = new Button
        {
            Text = normalText,
            X = x,
            Y = y,
            NoDecorations = true,
            NoPadding = true,
            ShadowStyle = ShadowStyles.None,
        };
        button.SetScheme(CreatePrimaryActionScheme());
        button.HasFocusChanged += (_, _) => button.Text = button.HasFocus ? focusedText : normalText;
        return button;
    }

    private static Scheme CreateInputScheme()
    {
        Attribute normal = TerminalGUITheme.Base.Normal;
        Attribute selected = TerminalGUITheme.Selected.Normal;
        return new Scheme
        {
            Normal = normal,
            Focus = selected,
            HotNormal = normal,
            HotFocus = selected,
            Active = selected,
            Editable = normal,
            ReadOnly = normal,
        };
    }

    private static Scheme CreateChoiceScheme()
    {
        return CreateInputScheme();
    }

    private static Scheme CreatePrimaryActionScheme()
    {
        Attribute normal = TerminalGUITheme.Label.Normal;
        Attribute selected = TerminalGUITheme.Selected.Normal;

        return new Scheme
        {
            Normal = normal,
            Focus = selected,
            HotNormal = normal,
            HotFocus = selected,
            Active = selected,
            Editable = normal,
            ReadOnly = normal,
            Disabled = normal,
        };
    }

    private sealed class OptionalGroupChoice
    {
        public string Name { get; }
        public Button Button { get; }
        public Label Description { get; }
        public bool IsSelected { get; private set; }

        public OptionalGroupChoice(string name, string? description, int row)
        {
            Name = name;
            Button = new Button
            {
                X = 0,
                Y = row,
                Width = Dim.Fill(),
                NoDecorations = true,
                NoPadding = true,
                ShadowStyle = ShadowStyles.None,
                TextAlignment = Alignment.Start,
            };
            Button.SetScheme(CreateChoiceScheme());
            Description = new Label
            {
                Text = description ?? string.Empty,
                X = 6,
                Y = row + 1,
                Width = Dim.Fill(),
            };
            Description.SetScheme(TerminalGUITheme.Muted);
            Button.KeyDown += (_, key) =>
            {
                if (key != Key.Space)
                {
                    return;
                }

                key.Handled = true;
                IsSelected = !IsSelected;
                UpdateText();
            };
            Button.HasFocusChanged += (_, _) => UpdateText();
            UpdateText();
        }

        private void UpdateText()
        {
            string indicator = Button.HasFocus ? ">" : " ";
            string checkbox = IsSelected ? "[*]" : "[ ]";
            Button.Text = $"{indicator} {checkbox} {Name}";
        }
    }

    private static (string Label, string Value)[] GetPreviewSummaryRows(InstallPreviewResult result)
    {
        return
        [
            (LocalizedStrings.Summary_Mod, result.ModName),
            (LocalizedStrings.Summary_Version, result.ModVersion),
            (LocalizedStrings.Summary_Author, result.ModAuthor),
        ];
    }

    private int AddPreviewTargets(IReadOnlyList<InstallChange> patches, int row)
    {
        var heading = new Label { Text = LocalizedStrings.InstallPreview_Targets, X = 0, Y = row };
        heading.SetScheme(TerminalGUITheme.SectionHeader);
        _form.Add(heading);
        row += 2;

        foreach (InstallChange patch in patches)
        {
            string name = $"- {patch.Name}:";
            var nameLabel = new Label { Text = name, X = 0, Y = row };
            var pathLabel = new Label
            {
                Text = patch.Path,
                X = GetDisplayWidth(name) + 1,
                Y = row,
                Width = Dim.Fill(),
            };
            pathLabel.SetScheme(TerminalGUITheme.Muted);
            _form.Add(nameLabel, pathLabel);
            row++;
        }

        return row;
    }

    private static string FormatPreviewVerboseDetails(InstallPreviewResult result, bool verbose)
    {
        if (!verbose)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        AppendTiming(text, result.Timing);
        return text.ToString().TrimEnd();
    }

    private static (string Label, string Value)[] GetResultSummaryRows(InstallModResult result)
    {
        var patches = GetChanges(result.Changes, InstallChangeKind.Patch);
        var payloads = GetChanges(result.Changes, InstallChangeKind.Payload);
        return
        [
            (LocalizedStrings.Summary_Mod, result.ModName),
            (LocalizedStrings.Summary_Version, result.ModVersion),
            (LocalizedStrings.InstallResult_PatchedFiles, patches.Length.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.InstallResult_CopiedFiles, payloads.Length.ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.Summary_Assets,
                patches.Sum(change => change.AssetCount).ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.Summary_Operations,
                patches.Sum(change => change.OperationCount).ToString(CultureInfo.InvariantCulture)),
            (LocalizedStrings.Summary_Elapsed, FormatElapsed(result.Timing.Elapsed)),
        ];
    }

    private static string FormatResultDetails(InstallModResult result, bool verbose)
    {
        var patches = GetChanges(result.Changes, InstallChangeKind.Patch);
        var payloads = GetChanges(result.Changes, InstallChangeKind.Payload);
        var text = new StringBuilder();
        if (patches.Length > 0)
        {
            text.AppendLine(LocalizedStrings.InstallResult_PatchedFiles);
            foreach (InstallChange change in patches)
            {
                text.Append("- ").Append(change.Name).Append(": ")
                    .Append(change.AssetCount.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(LocalizedStrings.Summary_AssetUnit).Append(", ")
                    .Append(change.OperationCount.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .AppendLine(LocalizedStrings.Summary_OperationUnit);
                text.Append("  ").Append(LocalizedStrings.InstallResult_Backup).Append(' ')
                    .AppendLine(change.BackupPath ?? string.Empty);
            }
        }

        if (payloads.Length > 0)
        {
            text.AppendLine().AppendLine(LocalizedStrings.InstallResult_CopiedFiles);
            foreach (InstallChange change in payloads)
            {
                text.Append("- ").AppendLine(Path.GetFileName(change.Path));
            }
        }

        if (result.OptionalGroups.Count > 0)
        {
            text.AppendLine().AppendLine(LocalizedStrings.InstallResult_OptionalContent);
            foreach (string group in result.OptionalGroups)
            {
                text.Append("- ").AppendLine(group);
            }
        }

        if (verbose)
        {
            AppendTiming(text, result.Timing);
        }

        return text.ToString().TrimEnd();
    }

    private static TableView CreateSummaryTable(
        IReadOnlyList<(string Label, string Value)> rows,
        int row)
    {
        const int summaryColumnGap = 3;
        int labelColumnWidth = rows.Max(item => GetDisplayWidth(item.Label)) + summaryColumnGap;
        var table = new TableView
        {
            X = 0,
            Y = row,
            Width = Dim.Fill(),
            Height = rows.Count,
            CanFocus = false,
            BorderStyle = LineStyle.None,
            Table = new SummaryTableSource(rows),
            Style = new TableStyle
            {
                ShowHeaders = false,
                AlwaysShowHeaders = false,
                ShowHorizontalBottomLine = false,
                ShowHorizontalHeaderOverline = false,
                ShowHorizontalHeaderUnderline = false,
                ShowVerticalCellLines = false,
                ShowVerticalCellLineForFirstColumn = false,
                ShowVerticalCellLineForLastColumn = false,
                ShowVerticalHeaderLines = false,
                InvertSelectedCellFirstCharacter = false,
                ExpandLastColumn = false,
                ColumnStyles =
                {
                    [0] = new ColumnStyle
                    {
                        MinWidth = labelColumnWidth,
                        MaxWidth = labelColumnWidth,
                        ColorGetter = _ => TerminalGUITheme.Muted,
                    },
                    [1] = new ColumnStyle
                    {
                        ColorGetter = _ => TerminalGUITheme.Base,
                    },
                },
            },
        };
        table.SetScheme(TerminalGUITheme.Base);
        return table;
    }

    private static void AppendTiming(StringBuilder text, TimingSnapshot snapshot)
    {
        text.AppendLine().AppendLine(LocalizedStrings.Install_TimingHeader);
        foreach (TimingStep step in snapshot.Steps)
        {
            text.Append(step.Name).Append("  ").AppendLine(FormatElapsed(step.Elapsed));
        }
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return $"{elapsed.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s";
    }

    private static InstallChange[] GetChanges(IReadOnlyList<InstallChange> changes, InstallChangeKind kind)
    {
        return changes.Where(change => change.Kind == kind).ToArray();
    }

    private static int GetDisplayWidth(string value)
    {
        return value.Sum(character => character is >= '\u1100' and <= '\u115f' or >= '\u2e80' and <= '\ua4cf'
            or >= '\uac00' and <= '\ud7a3' or >= '\uf900' and <= '\ufaff' or >= '\ufe10' and <= '\ufe19'
            or >= '\ufe30' and <= '\ufe6f' or >= '\uff00' and <= '\uff60' or >= '\uffe0' and <= '\uffe6'
            ? 2
            : 1);
    }

    private static int GetReportHeight(string text)
    {
        const int maximumVisibleLines = 20;
        int lineCount = text.Count(character => character == '\n') + 1;
        return Math.Min(lineCount, maximumVisibleLines);
    }

    private sealed class SummaryTableSource : ITableSource
    {
        private readonly IReadOnlyList<(string Label, string Value)> _rows;

        public SummaryTableSource(IReadOnlyList<(string Label, string Value)> rows)
        {
            _rows = rows;
        }

        public string[] ColumnNames => [string.Empty, string.Empty];

        public int Columns => 2;

        public int Rows => _rows.Count;

        public object this[int row, int col] => col == 0 ? _rows[row].Label : _rows[row].Value;
    }
}
