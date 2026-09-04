using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Pages.RepositoryIssue;

public sealed class RepositoryIssueView : View, ITerminalRenderRequester
{
    public event EventHandler? RenderRequested;

    private readonly LocalizedStrings _strings;
    private readonly RepositoryIssueLogic _logic;
    private readonly Action _exit;
    private readonly ScrollableContentView _content;
    private View? _preferredFocus;
    private bool _isDisposed;

    internal RepositoryIssueView(
        LocalizedStrings strings,
        RepositoryIssueLogic logic,
        Action exit)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(logic);
        ArgumentNullException.ThrowIfNull(exit);

        _strings = strings;
        _logic = logic;
        _exit = exit;

        _content = new ScrollableContentView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        Add(_content);
        RenderState();

        Initialized += (_, _) => _preferredFocus?.SetFocus();
        Disposing += (_, _) => _isDisposed = true;
    }

    private void RenderState()
    {
        if (_isDisposed)
        {
            return;
        }

        _preferredFocus = null;
        _content.RemoveAllAndDispose();

        switch (_logic.State)
        {
            case RepositoryIssueState.UnsupportedFormat state:
                RenderUnsupportedFormat(state);
                break;
            case RepositoryIssueState.ClearConfirmation:
                RenderClearConfirmation();
                break;
            case RepositoryIssueState.RecoveryProblem state:
                RenderRecoveryProblem(state.Report);
                break;
            case RepositoryIssueState.RecoveryPreview state:
                RenderRecoveryPreview(state.Preview);
                break;
            case RepositoryIssueState.Unchecked:
            case RepositoryIssueState.Ready:
                throw new InvalidOperationException(
                    $"Repository issue view cannot render state '{_logic.State.GetType().Name}'.");
        }
    }

    private void RenderUnsupportedFormat(RepositoryIssueState.UnsupportedFormat state)
    {
        AddLabel(_strings.RepositoryFormat_UnsupportedTitle, 0, TextRole.Error);
        AddLabel(_strings.RepositoryFormat_UnsupportedDescription, 2, TextRole.Preview);
        AddLabel(
            _strings.RepositoryFormat_UnsupportedVersionFormat(
                state.ActualVersion,
                state.SupportedVersion),
            4,
            TextRole.Error);
        AddLabel(_strings.RepositoryFormat_ClearWarning, 6, TextRole.Error);

        int actionRow = 9;
        if (state.ClearError is { } error)
        {
            AddLabel(
                _strings.RepositoryFormat_ClearFailedFormat(
                    OperationErrorFormatter.Format(_strings, error)),
                8,
                TextRole.Error);
            actionRow = 11;
        }

        var clearChoice = new ChoiceItem(
            _strings.RepositoryFormat_ClearAction,
            _strings.RepositoryFormat_ClearDescription)
        {
            X = 0,
            Y = actionRow
        };
        clearChoice.Button.Accepted += (_, _) => Transition(_logic.ShowClearConfirmation);

        var exitChoice = new ChoiceItem(
            _strings.RepositoryFormat_ExitAction,
            _strings.RepositoryFormat_ExitDescription)
        {
            X = 0,
            Y = actionRow + 2
        };
        exitChoice.Button.Accepted += (_, _) => _exit();

        _content.Add(clearChoice, exitChoice);
        ChoiceItem.AlignDescriptions([clearChoice, exitChoice]);
        FinishScreen(actionRow + 5, clearChoice.Button);
    }

    private void RenderClearConfirmation()
    {
        AddLabel(_strings.RepositoryFormat_ConfirmTitle, 0, TextRole.Error);
        AddLabel(_strings.RepositoryFormat_ConfirmDescription, 2, TextRole.Error);

        var actions = new ConfirmationBar(
            _strings.RepositoryFormat_ConfirmAction,
            () => RunLogicAsync(_logic.ClearAsync),
            _strings.RepositoryFormat_CancelAction,
            () => Transition(_logic.CancelClearConfirmation),
            ActionKind.Dangerous)
        {
            X = 0,
            Y = 5
        };
        _content.Add(actions);
        FinishScreen(7, actions.CancelButton);
    }

    private void RenderRecoveryProblem(RepositoryRecoveryReport recovery)
    {
        string details = recovery.Issues.Count == 0
            ? _strings.RepositoryRecovery_InterruptedOperation
            : string.Join(Environment.NewLine, recovery.Issues.Select(issue =>
                OperationErrorFormatter.Format(_strings, issue)));
        AddLabel(_strings.RepositoryRecovery_DamagedTitle, 0, TextRole.Error);
        AddLabel(_strings.RepositoryRecovery_DamagedDescription, 2, TextRole.Preview);
        AddLabel(details, 4, TextRole.Error);

        var choices = new List<ChoiceItem>();
        View focus;

        if (recovery.Status == RepositoryRecoveryStatus.RecoveryRequired)
        {
            var input = new InputField
            {
                X = 0,
                Y = 7,
                Width = Dim.Fill()
            };
            var previewChoice = new ChoiceItem(
                _strings.RepositoryRecovery_PreviewAction,
                _strings.RepositoryRecovery_PreviewDescription)
            {
                X = 0,
                Y = 9
            };
            previewChoice.Button.Accepted += async (_, _) => await PreviewRecoveryAsync(input);
            input.Accepted += (_, _) => previewChoice.Button.SetFocus();

            AddLabel($"{_strings.RepositoryRecovery_GameDirectoryPrompt}: ", 6, TextRole.Label);
            _content.Add(input, previewChoice);
            choices.Add(previewChoice);
            focus = input;
        }
        else
        {
            var retryChoice = new ChoiceItem(
                _strings.RepositoryRecovery_RetryAction,
                _strings.RepositoryRecovery_RetryDescription)
            {
                X = 0,
                Y = 7
            };
            retryChoice.Button.Accepted += async (_, _) =>
                await RunLogicAsync(_logic.InitializeAsync);
            _content.Add(retryChoice);
            choices.Add(retryChoice);
            focus = retryChoice.Button;
        }

        var exitChoice = new ChoiceItem(
            _strings.RepositoryRecovery_ExitAction,
            _strings.RepositoryRecovery_ExitDescription)
        {
            X = 0,
            Y = 12
        };
        exitChoice.Button.Accepted += (_, _) => _exit();
        _content.Add(exitChoice);
        choices.Add(exitChoice);
        ChoiceItem.AlignDescriptions(choices);
        FinishScreen(15, focus);
    }

    private void RenderRecoveryPreview(RepositoryRecoveryPreview preview)
    {
        string summary = $"{preview.Kind} {preview.InstallId} — {preview.Action}";
        AddLabel(_strings.RepositoryRecovery_PreviewTitle, 0, TextRole.Preview);
        AddLabel(preview.GameDirectory ?? string.Empty, 2, TextRole.Label);
        AddLabel(summary, 4, TextRole.Preview);
        AddLabel(
            string.Join(Environment.NewLine, preview.Files.Select(file =>
                $"- {file.Action}: {file.RelativePath}")),
            6,
            TextRole.Label);

        int actionRow = 8 + preview.Files.Count;
        var choices = new List<ChoiceItem>();
        View? focus = null;

        if (preview.CanRecover)
        {
            var applyChoice = new ChoiceItem(
                _strings.RepositoryRecovery_ApplyAction,
                _strings.RepositoryRecovery_ApplyDescription)
            {
                X = 0,
                Y = actionRow
            };
            applyChoice.Button.Accepted += async (_, _) =>
                await RunLogicAsync(_logic.RecoverAsync);
            _content.Add(applyChoice);
            choices.Add(applyChoice);
            focus = applyChoice.Button;
        }

        var backChoice = new ChoiceItem(
            _strings.RepositoryRecovery_BackAction,
            _strings.RepositoryRecovery_BackDescription)
        {
            X = 0,
            Y = actionRow + 2
        };
        backChoice.Button.Accepted += (_, _) => Transition(_logic.BackToRecovery);

        var exitChoice = new ChoiceItem(
            _strings.RepositoryRecovery_ExitAction,
            _strings.RepositoryRecovery_ExitDescription)
        {
            X = 0,
            Y = actionRow + 4
        };
        exitChoice.Button.Accepted += (_, _) => _exit();

        _content.Add(backChoice, exitChoice);
        choices.Add(backChoice);
        choices.Add(exitChoice);
        ChoiceItem.AlignDescriptions(choices);
        FinishScreen(actionRow + 7, focus ?? backChoice.Button);
    }

    private async Task PreviewRecoveryAsync(InputField input)
    {
        string path = TerminalPathNormalizer.Normalize(input.Text);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await RunLogicAsync(() => _logic.PreviewRecoveryAsync(path));
        }
    }

    private async Task RunLogicAsync(Func<Task> startOperation)
    {
        if (_logic.IsWorking || _isDisposed)
        {
            return;
        }

        Task operation = startOperation();
        await operation;

        if (_isDisposed || _logic.State is RepositoryIssueState.Ready)
        {
            return;
        }

        RenderState();
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Transition(Action transition)
    {
        if (_logic.IsWorking || _isDisposed)
        {
            return;
        }

        transition();
        RenderState();
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AddLabel(string text, Pos y, TextRole role)
    {
        _content.Add(new StyledLabel(text, role)
        {
            X = 0,
            Y = y,
            Width = Dim.Fill()
        });
    }

    private void FinishScreen(int rowCount, View focus)
    {
        _content.SetContentHeightForRows(rowCount);
        _preferredFocus = focus;

        if (IsInitialized)
        {
            focus.SetFocus();
        }
    }
}
