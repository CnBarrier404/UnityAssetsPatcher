using System.Globalization;
using System.Text;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Installation;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class InstallModView : View, ITerminalRenderRequester
{
    public event EventHandler? RenderRequested;

    private readonly IWorkflowService _workflowService;
    private readonly TerminalSettings _settings;
    private readonly Action _returnToMainMenu;
    private readonly InputField _modPath;
    private readonly StyledLabel _message;
    private readonly View _form;
    private InputField? _gameDirectory;
    private View? _optionalGroupArea;
    private readonly List<ToggleItem> _optionalGroups = [];
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

        var heading = new StyledLabel(
            LocalizedStrings.MainMenu_InstallMod_Title, TextRole.Title) { X = 0, Y = 0 };
        var description = new StyledLabel(
            LocalizedStrings.MainMenu_InstallMod_Description, TextRole.Muted)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
        };

        _form = new View
        {
            X = 0,
            Y = 3,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
        };
        string pathPrompt = $"{LocalizedStrings.InstallPage_ModZipPathPrompt}: ";
        var pathLabel = new StyledLabel(pathPrompt, TextRole.Label) { X = 0, Y = 0 };
        _modPath = new InputField
        {
            X = pathPrompt.GetColumns(),
            Y = 0,
            Width = Dim.Fill(),
        };
        _modPath.Accepted += (_, _) => Preview();
        _message = new StyledLabel
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
            var label = new StyledLabel(prompt, TextRole.Label) { X = 0, Y = gameDirectoryRow };
            _gameDirectory = new InputField
            {
                X = prompt.GetColumns(),
                Y = gameDirectoryRow,
                Width = Dim.Fill(),
            };
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
        var heading = new StyledLabel(
            LocalizedStrings.InstallPage_OptionalGroupsHeader, TextRole.Preview) { X = 0, Y = 0 };
        _optionalGroupArea.Add(heading);

        for (int index = 0; index < groups.Count; index++)
        {
            (string name, string? description) = groups[index];
            int choiceRow = 2 + (index * 2);
            var choice = new ToggleItem(name, description) { X = 0, Y = choiceRow };
            _optionalGroups.Add(choice);
            _optionalGroupArea.Add(choice);
        }

        int actionsRow = 3 + (groups.Count * 2);
        var actions = new ConfirmationBar(
            LocalizedStrings.InstallPage_SubmitAction,
            Preview,
            LocalizedStrings.InstallPage_BackAction,
            _returnToMainMenu)
        {
            X = 0,
            Y = actionsRow,
        };
        _optionalGroupArea.Add(actions);

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
        var status = new StyledLabel(
            LocalizedStrings.InstallPreview_DryRunStatus, TextRole.Preview) { X = 0, Y = 0 };
        var summary = new SummaryTableView(summaryRows) { X = 0, Y = 2 };
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
            var output = new StyledLabel(verboseText)
            {
                X = 0,
                Y = nextRow,
                Width = Dim.Fill(),
                Height = detailsHeight,
            };
            _form.Add(output);
            nextRow += detailsHeight + 1;
        }

        var actions = new ConfirmationBar(
            LocalizedStrings.InstallPage_InstallAction,
            () => Install(modPath, gameDirectory, selectedGroups),
            LocalizedStrings.InstallPage_BackAction,
            _returnToMainMenu)
        {
            X = 0,
            Y = nextRow + 1,
        };
        _form.Add(actions);
        actions.ConfirmButton.SetFocus();
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
        var status = new StyledLabel(
            LocalizedStrings.InstallResult_InstalledStatus, TextRole.Success) { X = 0, Y = 0 };
        var summary = new SummaryTableView(summaryRows) { X = 0, Y = 2 };
        int detailsRow = summaryRows.Length + 3;
        int actionRow = detailsRow + detailsHeight + 1;
        Button back = CreateActionButton(LocalizedStrings.InstallPage_ReturnAction, 0, actionRow);
        back.Accepted += (_, _) => _returnToMainMenu();
        _form.Add(status, summary);
        if (!string.IsNullOrEmpty(text))
        {
            _form.Add(new TextViewer(text)
            {
                X = 0,
                Y = detailsRow,
                Width = Dim.Fill(),
                Height = detailsHeight,
            });
        }

        _form.Add(back);
        back.SetFocus();
    }

    private void ShowResult(string text, bool isError)
    {
        _form.RemoveAll();
        int outputHeight = GetReportHeight(text);
        var output = new StyledLabel(
            text, isError ? TextRole.Error : TextRole.Base)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = outputHeight,
        };
        Button back = CreateActionButton(LocalizedStrings.InstallPage_ReturnAction, 0, outputHeight + 1);
        back.Accepted += (_, _) => _returnToMainMenu();
        _form.Add(output, back);
        back.SetFocus();
    }

    private void ShowInfo(string message)
    {
        _message.Visible = true;
        _message.Text = message;
        _message.SetScheme(TerminalTheme.Muted);
    }

    private void ShowError(string message)
    {
        _message.Visible = true;
        _message.Text = message;
        _message.SetScheme(TerminalTheme.Error);
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

    private static ActionButton CreateActionButton(string text, Pos x, Pos y)
    {
        return new ActionButton(text) { X = x, Y = y };
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
        var heading = new StyledLabel(
            LocalizedStrings.InstallPreview_Targets, TextRole.SectionHeader) { X = 0, Y = row };
        _form.Add(heading);
        row += 2;

        foreach (InstallChange patch in patches)
        {
            string name = $"- {patch.Name}:";
            var nameLabel = new StyledLabel(name) { X = 0, Y = row };
            var pathLabel = new StyledLabel(patch.Path, TextRole.Muted)
            {
                X = name.GetColumns() + 1,
                Y = row,
                Width = Dim.Fill(),
            };
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

    private static int GetReportHeight(string text)
    {
        const int maximumVisibleLines = 20;
        int lineCount = text.Count(character => character == '\n') + 1;
        return Math.Min(lineCount, maximumVisibleLines);
    }
}
