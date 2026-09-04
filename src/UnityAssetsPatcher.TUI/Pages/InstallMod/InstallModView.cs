using System.Globalization;
using System.Text;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Features.Install;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Patching;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI.Pages.InstallMod;

public sealed class InstallModView : TerminalOperationPageView
{
    protected override bool CanReturnToMainMenu => !_logic.IsWorking;

    private readonly LocalizedStrings _strings;
    private readonly InstallModLogic _logic;
    private readonly ScrollableContentView _form;

    internal InstallModView(
        LocalizedStrings strings,
        InstallModLogic logic)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(logic);

        _strings = strings;
        _logic = logic;

        SetHeader(_strings.MainMenu_InstallMod_Title, _strings.MainMenu_InstallMod_Description);

        _form = new ScrollableContentView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        Add(_form);
        RenderState();

        Initialized += async (_, _) =>
        {
            RequestRender();
            await SelectPackageAsync();
        };
        Disposing += (_, _) => _logic.Dispose();
    }

    protected override void RenderState()
    {
        if (IsDisposed)
        {
            return;
        }

        _form.RemoveAllAndDispose();

        switch (_logic.State)
        {
            case InstallModState.SelectPackage state:
                RenderPackageSelection(state.Error is null
                    ? null
                    : OperationErrorFormatter.Format(_strings, state.Error));
                break;
            case InstallModState.Analyzing:
                RenderWorking(_strings.InstallPage_AnalyzingMod);
                break;
            case InstallModState.EnterGameDirectory state:
                RenderGameDirectory(state);
                break;
            case InstallModState.SelectOptionalGroups state:
                RenderOptionalGroups(state);
                break;
            case InstallModState.Preview state:
                RenderPreview(state);
                break;
            case InstallModState.Installing:
                RenderWorking(_strings.InstallPage_InstallingMod);
                break;
            case InstallModState.Installed state:
                RenderInstalled(state.Result);
                break;
            case InstallModState.InstallFailed state:
                RenderInstallFailure(OperationErrorFormatter.Format(_strings, state.Error));
                break;
        }
    }

    private async Task SelectPackageAsync()
    {
        if (_logic.IsWorking || IsDisposed)
        {
            return;
        }

        string? path = WindowsNativeFilePicker.PickFile(
            _strings.InstallPage_SelectModDialogTitle,
            _strings.InstallPage_ModZipFileType);

        if (string.IsNullOrWhiteSpace(path))
        {
            RequestMainMenu();
            return;
        }

        path = TerminalPathNormalizer.Normalize(path);
        if (string.IsNullOrWhiteSpace(path))
        {
            RenderPackageSelection(
                _strings.Prompt_LabelRequiredFormat(_strings.InstallPage_SelectModAction));
            return;
        }

        if (!File.Exists(path))
        {
            RenderPackageSelection(_strings.Prompt_FileNotFoundFormat(path));
            return;
        }

        await RunLogicAsync(() => _logic.PreviewPackageAsync(path));
    }

    private async Task SubmitGameDirectoryAsync(
        InputField input,
        StyledLabel message)
    {
        string directory = TerminalPathNormalizer.Normalize(input.Text);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            input.Text = string.Empty;
            message.Text = _strings.Prompt_DirectoryNotFoundFormat(directory);
            message.SetRole(TextRole.Error);
            input.SetFocus();
            return;
        }

        input.Text = directory;
        await RunLogicAsync(() => _logic.SubmitGameDirectoryAsync(directory));
    }

    private async Task SubmitOptionalGroupsAsync(IReadOnlyList<ToggleItem> groups)
    {
        string[] selectedGroups = groups
            .Where(group => group.IsSelected)
            .Select(group => group.Name)
            .ToArray();

        await RunLogicAsync(() => _logic.SubmitOptionalGroupsAsync(selectedGroups));
    }

    private void RenderPackageSelection(string? error)
    {
        ActionButton select = AddButton(_strings.InstallPage_SelectModAction, 0, SelectPackageAsync);

        if (!string.IsNullOrEmpty(error))
        {
            AddLabel(error, 2, TextRole.Error);
        }

        FinishScreen(string.IsNullOrEmpty(error) ? 2 : 4, select);
    }

    private void RenderWorking(string message)
    {
        _form.Add(new WorkingIndicator(message) { X = 0, Y = 0 });
        FinishScreen(2);
    }

    private void RenderGameDirectory(InstallModState.EnterGameDirectory state)
    {
        ActionButton select = AddButton(
            _strings.InstallPage_SelectModAction,
            0,
            SelectPackageAsync);

        string prompt = $"{_strings.InstallPage_GameDirectoryPrompt}: ";
        var label = new StyledLabel(prompt, TextRole.Label) { X = 0, Y = 2 };
        var input = new InputField
        {
            X = prompt.GetColumns(),
            Y = 2,
            Width = Dim.Fill()
        };
        input.Text = state.Directory ?? string.Empty;
        StyledLabel message = AddLabel(
            OperationErrorFormatter.Format(_strings, state.Error),
            4,
            state.IsPrompt ? TextRole.Muted : TextRole.Error);

        input.Accepted += async (_, _) => await SubmitGameDirectoryAsync(input, message);

        _form.Add(label, input);
        FinishScreen(6, state.IsPrompt ? input : select);
    }

    private void RenderOptionalGroups(InstallModState.SelectOptionalGroups state)
    {
        ActionButton select = AddButton(
            _strings.InstallPage_SelectModAction,
            0,
            SelectPackageAsync);
        AddLabel(_strings.InstallPage_OptionalGroupsHeader, 3, TextRole.Preview);

        var choices = new List<ToggleItem>(state.Groups.Count);
        for (int index = 0; index < state.Groups.Count; index++)
        {
            (string name, string? description) = state.Groups[index];
            var choice = new ToggleItem(name, description)
            {
                X = 0,
                Y = 5 + index * 2,
                IsSelected = state.SelectedGroups.Contains(name)
            };
            choices.Add(choice);
            _form.Add(choice);
        }

        int actionsRow = 6 + state.Groups.Count * 2;
        AddConfirmation(
            _strings.InstallPage_SubmitAction,
            () => SubmitOptionalGroupsAsync(choices),
            actionsRow);

        if (state.Error is { } error)
        {
            AddLabel(
                OperationErrorFormatter.Format(_strings, error),
                actionsRow + 2,
                TextRole.Error);
        }

        FinishScreen(
            actionsRow + (state.Error is null ? 2 : 4),
            state.Error is null ? choices[0].Button : select);
    }

    private void RenderPreview(InstallModState.Preview state)
    {
        InstallPreviewResult result = state.Result;
        var summaryRows = GetPreviewSummaryRows(result);
        AddLabel(_strings.InstallPreview_DryRunStatus, 0, TextRole.Preview);
        _form.Add(new SummaryTableView(summaryRows) { X = 0, Y = 2 });

        if (state.BlockingDiagnostic is { } diagnostic)
        {
            RenderBlockedPreview(diagnostic, summaryRows.Length);
            return;
        }

        int nextRow = summaryRows.Length + 3;
        var patches = result.Changes
            .Where(change => change.Kind == InstallChangeKind.Patch)
            .ToArray();

        if (patches.Length > 0)
        {
            nextRow = AddPreviewTargets(patches, nextRow);
        }

        string verboseText = FormatPreviewVerboseDetails(result);
        if (!string.IsNullOrEmpty(verboseText))
        {
            int detailsHeight = GetReportHeight(verboseText);
            AddLabel(verboseText, nextRow, height: detailsHeight);
            nextRow += detailsHeight + 1;
        }

        ConfirmationBar actions = AddConfirmation(
            _strings.InstallPage_InstallAction,
            () => RunLogicAsync(_logic.InstallAsync),
            nextRow + 1);
        FinishScreen(nextRow + 3, actions.ConfirmButton);
    }

    private void RenderBlockedPreview(PatchDiagnostic diagnostic, int summaryRowCount)
    {
        string message = _strings.InstallPreview_PlanningFailedFormat(
            OperationErrorFormatter.Format(_strings, diagnostic));
        AddLabel(message, summaryRowCount + 3, TextRole.Error);
        ActionButton back = AddButton(
            _strings.InstallPage_BackAction,
            summaryRowCount + 5,
            RequestMainMenu);
        FinishScreen(summaryRowCount + 7, back);
    }

    private void RenderInstalled(InstallModResult result)
    {
        var summaryRows = GetResultSummaryRows(result);
        string text = FormatResultDetails(result);
        int detailsHeight = string.IsNullOrEmpty(text) ? 0 : GetReportHeight(text);
        AddLabel(_strings.InstallResult_InstalledStatus, 0, TextRole.Success);
        _form.Add(new SummaryTableView(summaryRows) { X = 0, Y = 2 });
        int detailsRow = summaryRows.Length + 3;
        int actionRow = detailsRow + detailsHeight + 1;

        if (!string.IsNullOrEmpty(text))
        {
            _form.Add(new TextViewer(text)
            {
                X = 0,
                Y = detailsRow,
                Width = Dim.Fill(),
                Height = detailsHeight
            });
        }

        ActionButton back = AddButton(
            _strings.InstallPage_ReturnAction,
            actionRow,
            RequestMainMenu);
        FinishScreen(actionRow + 2, back);
    }

    private void RenderInstallFailure(string text)
    {
        int outputHeight = GetReportHeight(text);
        AddLabel(text, 0, TextRole.Error, outputHeight);
        ActionButton back = AddButton(
            _strings.InstallPage_ReturnAction,
            outputHeight + 1,
            RequestMainMenu);
        FinishScreen(outputHeight + 3, back);
    }

    private int AddPreviewTargets(IReadOnlyList<InstallChange> patches, int row)
    {
        AddLabel(_strings.InstallPreview_Targets, row, TextRole.SectionHeader);
        row += 2;

        foreach (InstallChange patch in patches)
        {
            string name = $"- {patch.Name}:";
            _form.Add(
                new StyledLabel(name) { X = 0, Y = row },
                new StyledLabel(patch.Path, TextRole.Muted)
                {
                    X = name.GetColumns() + 1,
                    Y = row,
                    Width = Dim.Fill()
                });
            row++;
        }

        return row;
    }

    private (string Label, string Value)[] GetPreviewSummaryRows(InstallPreviewResult result)
    {
        return
        [
            (_strings.Summary_Mod, result.ModName),
            (_strings.Summary_Version, result.ModVersion),
            (_strings.Summary_Author, result.ModAuthor)
        ];
    }

    private (string Label, string Value)[] GetResultSummaryRows(InstallModResult result)
    {
        return
        [
            (_strings.Summary_Mod, result.ModName),
            (_strings.Summary_Version, result.ModVersion),
            (_strings.Summary_Elapsed, FormatElapsed(result.Timing.Elapsed))
        ];
    }

    private string FormatPreviewVerboseDetails(InstallPreviewResult result)
    {
        if (!_logic.VerboseLogging)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        AppendTiming(text, result.Timing);
        return text.ToString().TrimEnd();
    }

    private string FormatResultDetails(InstallModResult result)
    {
        if (result.Changes.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder text = new StringBuilder().AppendLine(_strings.InstallResult_OperatedFiles);

        foreach (InstallChange change in result.Changes)
        {
            string path = change.Kind == InstallChangeKind.Patch
                ? change.Name
                : Path.GetFileName(change.Path);
            text.Append("- ").AppendLine(path);
        }

        return text.ToString().TrimEnd();
    }

    private void AppendTiming(StringBuilder text, TimingSnapshot snapshot)
    {
        text.AppendLine().AppendLine(_strings.Install_TimingHeader);
        foreach (TimingStep step in snapshot.Steps)
        {
            text.Append(step.Name).Append("  ").AppendLine(FormatElapsed(step.Elapsed));
        }
    }

    private StyledLabel AddLabel(
        string text,
        Pos y,
        TextRole role = TextRole.Base,
        int? height = null)
    {
        var label = new StyledLabel(text, role)
        {
            X = 0,
            Y = y,
            Width = Dim.Fill()
        };
        if (height is { } value)
        {
            label.Height = value;
        }

        _form.Add(label);
        return label;
    }

    private ActionButton AddButton(string text, Pos y, Action action)
    {
        var button = new ActionButton(text) { X = 0, Y = y };
        button.Accepted += (_, _) => action();
        _form.Add(button);
        return button;
    }

    private ActionButton AddButton(string text, Pos y, Func<Task> action)
    {
        var button = new ActionButton(text) { X = 0, Y = y };
        button.Accepted += async (_, _) => await action();
        _form.Add(button);
        return button;
    }

    private ConfirmationBar AddConfirmation(string text, Func<Task> action, Pos y)
    {
        var confirmation = new ConfirmationBar(
            text,
            action,
            _strings.InstallPage_BackAction,
            RequestMainMenu)
        {
            X = 0,
            Y = y
        };
        _form.Add(confirmation);
        return confirmation;
    }

    private void FinishScreen(int rowCount, View? focus = null)
    {
        _form.SetContentHeightForRows(rowCount);
        focus?.SetFocus();
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return $"{elapsed.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s";
    }

    private static int GetReportHeight(string text)
    {
        const int maximumVisibleLines = 20;
        int lineCount = text.Count(character => character == '\n') + 1;
        return Math.Min(lineCount, maximumVisibleLines);
    }
}
