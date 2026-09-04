using System.Globalization;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Features.Uninstall;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages.UninstallMod;

public sealed class UninstallModView : TerminalOperationPageView
{
    protected override bool CanReturnToMainMenu => !_logic.IsWorking;

    private readonly LocalizedStrings _strings;
    private readonly UninstallModLogic _logic;
    private readonly ScrollableContentView _body;

    internal UninstallModView(
        LocalizedStrings strings,
        UninstallModLogic logic)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(logic);

        _strings = strings;
        _logic = logic;

        SetHeader(_strings.MainMenu_UninstallMod_Title, _strings.MainMenu_UninstallMod_Description);

        _body = new ScrollableContentView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        Add(_body);
        RenderState();

        Initialized += async (_, _) => await RunLogicAsync(_logic.LoadInstalledModsAsync);
        Disposing += (_, _) => _logic.Dispose();
    }

    protected override void RenderState()
    {
        if (IsDisposed)
        {
            return;
        }

        _body.RemoveAllAndDispose();

        switch (_logic.State)
        {
            case UninstallModState.LoadingInstalledMods:
                FinishScreen(0);
                break;
            case UninstallModState.InstalledMods state:
                RenderInstalledMods(state.Records);
                break;
            case UninstallModState.Analyzing:
                RenderWorking(_strings.UninstallPage_AnalyzingMod);
                break;
            case UninstallModState.EnterGameDirectory state:
                RenderGameDirectoryInput(state);
                break;
            case UninstallModState.Preview state:
                RenderPreview(state.Result);
                break;
            case UninstallModState.Uninstalling:
                RenderWorking(_strings.UninstallPage_UninstallingMod);
                break;
            case UninstallModState.Uninstalled state:
                RenderResult(state.Result);
                break;
            case UninstallModState.Failed state:
                RenderError(OperationErrorFormatter.Format(_strings, state.Error));
                break;
        }
    }

    private void RenderInstalledMods(IReadOnlyList<InstallRecordSummary> installed)
    {
        if (installed.Count == 0)
        {
            AddLabel(_strings.UninstallPage_NoInstalledModsFound, 0, TextRole.Preview);
            ActionButton back = AddButton(
                _strings.UninstallPage_ReturnAction,
                2,
                RequestMainMenu);
            FinishScreen(4, back);
            return;
        }

        int row = 0;
        var choices = new List<ChoiceItem>(installed.Count);
        foreach (InstallRecordSummary record in installed)
        {
            choices.Add(AddInstalledMod(record, row));
            row += 2;
        }

        ChoiceItem.AlignDescriptions(choices);
        FinishScreen(row, choices[0].Button);
    }

    private ChoiceItem AddInstalledMod(InstallRecordSummary record, int row)
    {
        string installedAt = FormatInstalledAt(record.InstalledAt);
        string details = record.GameName is null ? installedAt : $"{installedAt} | {record.GameName}";
        var choice = new ChoiceItem($"{record.ModName} {record.ModVersion}", details)
        {
            X = 0,
            Y = row
        };
        choice.Button.Accepted += async (_, _) =>
            await RunLogicAsync(() => _logic.PreviewAsync(record.InstallId));
        _body.Add(choice);
        return choice;
    }

    private void RenderWorking(string text)
    {
        _body.Add(new WorkingIndicator(text) { X = 0, Y = 0 });
        FinishScreen(2);
    }

    private void RenderGameDirectoryInput(UninstallModState.EnterGameDirectory state)
    {
        AddLabel(
            OperationErrorFormatter.Format(_strings, state.Error),
            0,
            TextRole.Muted);

        string prompt = $"{_strings.InstallPage_GameDirectoryPrompt}: ";
        var label = new StyledLabel(prompt, TextRole.Label) { X = 0, Y = 2 };
        var input = new InputField
        {
            X = prompt.GetColumns(),
            Y = 2,
            Width = Dim.Fill()
        };
        var error = new StyledLabel(role: TextRole.Error)
        {
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
            Visible = false
        };

        input.Accepted += async (_, _) =>
        {
            string path = TerminalPathNormalizer.Normalize(input.Text);
            if (!Directory.Exists(path))
            {
                input.Text = string.Empty;
                error.Text = _strings.Prompt_DirectoryNotFoundFormat(path);
                error.Visible = true;
                input.SetFocus();
                return;
            }

            input.Text = path;
            await RunLogicAsync(() => _logic.SubmitGameDirectoryAsync(path));
        };

        AddButton(
            _strings.UninstallPage_BackAction,
            6,
            () => RunLogicAsync(_logic.LoadInstalledModsAsync));

        _body.Add(label, input, error);
        FinishScreen(8, input);
    }

    private void RenderPreview(UninstallPreviewResult preview)
    {
        AddLabel(_strings.UninstallPreview_Status, 0, TextRole.Preview);
        var rows = new (string Label, string Value)[]
        {
            (_strings.Summary_Mod, preview.ModName),
            (_strings.Summary_Version, preview.ModVersion),
            (_strings.UninstallSummary_GameDirectory, preview.GameDirectory),
            (_strings.UninstallSummary_Installed, FormatInstalledAt(preview.InstalledAt))
        };
        _body.Add(new SummaryTableView(rows) { X = 0, Y = 2 });

        string availability = preview.CanUninstall
            ? _strings.UninstallPreview_WillModify
            : preview.DependencyFailures.Count > 0
                ? _strings.UninstallPreview_CannotRemoveDependency
                : _strings.UninstallPreview_CannotRemoveIntegrity;
        AddLabel(
            availability,
            rows.Length + 3,
            preview.CanUninstall ? TextRole.Success : TextRole.Error);

        int row = rows.Length + 5;
        row = AddChangedPreviewFiles(preview.ChangedFiles, row);
        row = AddDependencyFailures(preview.DependencyFailures, row);

        if (!preview.CanUninstall)
        {
            AddLabel(
                preview.DependencyFailures.Count > 0
                    ? _strings.UninstallPage_CannotUninstallDependency
                    : _strings.UninstallPage_CannotUninstallIntegrityConflict,
                row + 1,
                TextRole.Error);
            ActionButton back = AddButton(
                _strings.UninstallPage_BackAction,
                row + 3,
                () => RunLogicAsync(_logic.LoadInstalledModsAsync));
            FinishScreen(row + 5, back);
            return;
        }

        var actions = new ConfirmationBar(
            _strings.UninstallPage_UninstallAction,
            () => RunLogicAsync(_logic.UninstallAsync),
            _strings.UninstallPage_BackAction,
            () => RunLogicAsync(_logic.LoadInstalledModsAsync),
            ActionKind.Dangerous)
        {
            X = 0,
            Y = row + 1
        };
        _body.Add(actions);
        FinishScreen(row + 3, actions.ConfirmButton);
    }

    private int AddChangedPreviewFiles(IReadOnlyList<UninstallChangedFileResult> files, int row)
    {
        if (files.Count == 0)
        {
            return row;
        }

        foreach (UninstallChangedFileResult file in files)
        {
            AddLabel($"- {file.RelativePath}", row++, TextRole.Muted);
        }

        return row + 1;
    }

    private int AddDependencyFailures(
        IReadOnlyList<UninstallDependencyFailureResult> failures,
        int row)
    {
        if (failures.Count == 0)
        {
            return row;
        }

        row = AddSectionHeader(_strings.UninstallPreview_Dependencies, row);
        foreach (UninstallDependencyFailureResult failure in failures)
        {
            string diagnostic = $"{failure.Diagnostic.Code}: " +
                                OperationErrorFormatter.Format(_strings, failure.Diagnostic);
            AddLabel(
                _strings.UninstallPreview_DependencyDetailsFormat(
                    failure.ModName,
                    failure.ModVersion,
                    failure.RelativePath,
                    diagnostic),
                row++);
        }

        return row + 1;
    }

    private int AddSectionHeader(string text, int row)
    {
        AddLabel(text, row, TextRole.SectionHeader);
        return row + 2;
    }

    private void RenderResult(UninstallModResult result)
    {
        AddLabel(_strings.UninstallResult_Status, 0, TextRole.Success);
        var rows = new (string Label, string Value)[]
        {
            (_strings.Summary_Mod, result.ModName),
            (_strings.Summary_Version, result.ModVersion),
            (_strings.UninstallSummary_ChangedFiles,
                result.ChangedFiles.Count.ToString(CultureInfo.InvariantCulture))
        };
        _body.Add(new SummaryTableView(rows) { X = 0, Y = 2 });

        int row = rows.Length + 3;
        if (result.ChangedFiles.Count > 0)
        {
            row = AddSectionHeader(_strings.UninstallResult_ChangedFiles, row);
            foreach (UninstallChangedFileResult file in result.ChangedFiles)
            {
                AddLabel($"- {file.RelativePath}", row++);
            }

            row++;
        }

        ActionButton back = AddButton(
            _strings.UninstallPage_ReturnAction,
            row,
            RequestMainMenu);
        FinishScreen(row + 2, back);
    }

    private void RenderError(string message)
    {
        AddLabel(message, 0, TextRole.Error);
        ActionButton back = AddButton(
            _strings.UninstallPage_BackAction,
            2,
            () => RunLogicAsync(_logic.LoadInstalledModsAsync));
        FinishScreen(4, back);
    }

    private void AddLabel(string text, Pos y, TextRole role = TextRole.Base)
    {
        var label = new StyledLabel(text, role)
        {
            X = 0,
            Y = y,
            Width = Dim.Fill()
        };
        _body.Add(label);
    }

    private ActionButton AddButton(string text, Pos y, Action action)
    {
        var button = new ActionButton(text) { X = 0, Y = y };
        button.Accepted += (_, _) => action();
        _body.Add(button);
        return button;
    }

    private ActionButton AddButton(string text, Pos y, Func<Task> action)
    {
        var button = new ActionButton(text) { X = 0, Y = y };
        button.Accepted += async (_, _) => await action();
        _body.Add(button);
        return button;
    }

    private void FinishScreen(int rowCount, View? focus = null)
    {
        _body.SetContentHeightForRows(rowCount);
        focus?.SetFocus();
    }

    private static string FormatInstalledAt(DateTimeOffset installedAt)
    {
        return installedAt.LocalDateTime.ToString(
            "yyyy'/'MM'/'dd HH':'mm",
            CultureInfo.InvariantCulture);
    }
}
