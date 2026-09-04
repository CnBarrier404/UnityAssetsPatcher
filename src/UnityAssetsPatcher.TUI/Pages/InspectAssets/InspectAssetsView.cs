using System.Globalization;
using System.Text;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Application.Features.Inspect;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Pages.InspectAssets;

public sealed class InspectAssetsView : TerminalPageView, ITerminalRenderRequester
{
    public event EventHandler? RenderRequested;

    protected override bool CanReturnToMainMenu => !_logic.IsWorking;

    private const int DefaultLimit = 100;

    private readonly LocalizedStrings _strings;
    private readonly InspectAssetsLogic _logic;
    private readonly View _body;
    private bool _isDisposed;

    internal InspectAssetsView(
        LocalizedStrings strings,
        InspectAssetsLogic logic)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(logic);

        _strings = strings;
        _logic = logic;

        _body = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        Add(_body);
        RenderState();

        Disposing += (_, _) =>
        {
            _isDisposed = true;
            _logic.Dispose();
        };
    }

    private void RenderState()
    {
        if (_isDisposed)
        {
            return;
        }

        _body.RemoveAllAndDispose();

        switch (_logic.State)
        {
            case InspectAssetsState.ActionMenu:
                SetActionMenuHeader();
                RenderActionMenu();
                break;
            case InspectAssetsState.EnterListPath:
                SetListPathHeader();
                RenderListPathInput();
                break;
            case InspectAssetsState.SelectLimit:
                SetListLimitHeader();
                RenderLimitChoices();
                break;
            case InspectAssetsState.EnterCustomLimit:
                SetListLimitHeader();
                RenderCustomLimitInput();
                break;
            case InspectAssetsState.EnterFields:
                SetFieldsHeader();
                RenderFieldsInput();
                break;
            case InspectAssetsState.Working state:
                SetOperationHeader(state.Operation);
                RenderWorking();
                break;
            case InspectAssetsState.Assets state:
                SetListLimitHeader();
                RenderAssets(state.Result);
                break;
            case InspectAssetsState.Fields state:
                SetFieldsHeader();
                RenderFields(state.FieldTree);
                break;
            case InspectAssetsState.Failed state:
                SetOperationHeader(state.Operation);
                RenderError(OperationErrorFormatter.Format(_strings, state.Error));
                break;
        }
    }

    private void RenderActionMenu()
    {
        ChoiceItem list = AddChoice(
            _strings.InspectPage_ListAssetsTitle,
            _strings.InspectPage_ListAssetsDescription,
            0);
        list.Button.Accepted += (_, _) => RunTransition(_logic.ShowListPathInput);

        ChoiceItem fields = AddChoice(
            _strings.InspectPage_ShowFieldsTitle,
            _strings.InspectPage_ShowFieldsDescription,
            2);
        fields.Button.Accepted += (_, _) => RunTransition(_logic.ShowFieldsInput);

        ChoiceItem.AlignDescriptions([list, fields]);
        list.Button.SetFocus();
    }

    private ChoiceItem AddChoice(string text, string description, int row)
    {
        var choice = new ChoiceItem(text, description)
        {
            X = 0,
            Y = row
        };

        _body.Add(choice);
        return choice;
    }

    private void RenderListPathInput()
    {
        string prompt = $"{_strings.InspectPage_AssetsFilePathPrompt}: ";
        var label = new StyledLabel(prompt, TextRole.Label)
        {
            X = 0,
            Y = 0
        };

        var input = new InputField
        {
            X = prompt.GetColumns(),
            Y = 0,
            Width = Dim.Fill()
        };

        var error = new StyledLabel(role: TextRole.Error)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Visible = false
        };

        input.Accepted += (_, _) =>
        {
            string path = TerminalPathNormalizer.Normalize(input.Text);

            if (!File.Exists(path))
            {
                input.Text = string.Empty;
                error.Text = _strings.Prompt_FileNotFoundFormat(path);
                error.Visible = true;
                input.SetFocus();
                return;
            }

            input.Text = path;
            RunTransition(() => _logic.SubmitListPath(Path.GetFullPath(path)));
        };

        ActionButton back = AddButton(
            _strings.InspectPage_BackAction,
            4,
            () => RunTransition(_logic.ShowActionMenu));
        _body.Add(label, input, error);
        input.SetFocus();
    }

    private void RenderLimitChoices()
    {
        ActionButton first = AddButton(
            _strings.InspectPage_First100Choice,
            0,
            () => RunLogicAsync(() => _logic.InspectListAsync(DefaultLimit)));
        AddButton(
            _strings.InspectPage_AllRowsChoice,
            2,
            () => RunLogicAsync(() => _logic.InspectListAsync(null)));
        AddButton(
            _strings.InspectPage_CustomLimitChoice,
            4,
            () => RunTransition(_logic.ShowCustomLimitInput));
        AddButton(
            _strings.InspectPage_BackAction,
            6,
            () => RunTransition(_logic.ShowListPathInput));
        first.SetFocus();
    }

    private void RenderCustomLimitInput()
    {
        string prompt = $"{_strings.InspectPage_MaximumRowsPrompt}: ";
        var label = new StyledLabel(prompt, TextRole.Label)
        {
            X = 0,
            Y = 0
        };
        var input = new InputField
        {
            X = prompt.GetColumns(),
            Y = 0,
            Width = 12
        };
        var error = new StyledLabel(role: TextRole.Error)
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Visible = false
        };

        input.Accepted += async (_, _) => await SubmitCustomLimitAsync(input, error);
        AddButton(
            _strings.InspectPage_BackAction,
            4,
            () => RunTransition(_logic.ReturnToLimitChoices));
        _body.Add(label, input, error);
        input.SetFocus();
    }

    private async Task SubmitCustomLimitAsync(InputField input, StyledLabel error)
    {
        if (!int.TryParse(
                input.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int limit) ||
            limit <= 0)
        {
            input.Text = string.Empty;
            error.Text = _strings.Prompt_InvalidPositiveIntegerFormat(
                _strings.InspectPage_MaximumRowsPrompt);
            error.Visible = true;
            input.SetFocus();
            return;
        }

        await RunLogicAsync(() => _logic.InspectListAsync(limit));
    }

    private void RenderFieldsInput()
    {
        string pathPrompt = $"{_strings.InspectPage_AssetsFilePathPrompt}: ";
        var pathLabel = new StyledLabel(pathPrompt, TextRole.Label)
        {
            X = 0,
            Y = 0
        };
        var pathInput = new InputField
        {
            X = pathPrompt.GetColumns(),
            Y = 0,
            Width = Dim.Fill()
        };
        string idPrompt = $"{_strings.InspectPage_PathIdPrompt}: ";
        var idLabel = new StyledLabel(idPrompt, TextRole.Label)
        {
            X = 0,
            Y = 2
        };
        var idInput = new InputField
        {
            X = idPrompt.GetColumns(),
            Y = 2,
            Width = 20
        };
        var error = new StyledLabel(role: TextRole.Error)
        {
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
            Visible = false
        };

        pathInput.Accepted += (_, _) => idInput.SetFocus();
        idInput.Accepted += async (_, _) =>
            await SubmitFieldsAsync(pathInput, idInput, error);
        AddButton(
            _strings.InspectPage_ShowFieldsTitle,
            6,
            () => SubmitFieldsAsync(pathInput, idInput, error),
            ActionKind.Primary);
        AddButton(
            _strings.InspectPage_BackAction,
            8,
            () => RunTransition(_logic.ShowActionMenu));
        _body.Add(pathLabel, pathInput, idLabel, idInput, error);
        pathInput.SetFocus();
    }

    private async Task SubmitFieldsAsync(
        InputField pathInput,
        InputField idInput,
        StyledLabel error)
    {
        string path = TerminalPathNormalizer.Normalize(pathInput.Text);
        if (!File.Exists(path))
        {
            pathInput.Text = string.Empty;
            error.Text = _strings.Prompt_FileNotFoundFormat(path);
            error.Visible = true;
            pathInput.SetFocus();
            return;
        }

        if (!long.TryParse(
                idInput.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long pathId))
        {
            idInput.Text = string.Empty;
            error.Text = _strings.Prompt_InvalidIntegerFormat(_strings.InspectPage_PathIdPrompt);
            error.Visible = true;
            idInput.SetFocus();
            return;
        }

        await RunLogicAsync(() =>
            _logic.InspectFieldsAsync(Path.GetFullPath(path), pathId));
    }

    private void RenderAssets(InspectListResult result)
    {
        var table = new DataTableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
            Table = new AssetsTableSource(_strings, result.Assets)
        };
        string infoText = result.Assets.Count < result.TotalCount
            ? _strings.InspectPage_ShowingAssetsFormat(result.Assets.Count, result.TotalCount)
            : string.Empty;
        var info = new StyledLabel(infoText, TextRole.Muted)
        {
            X = 0,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill()
        };
        ActionButton back = AddButton(
            _strings.InspectPage_ReturnAction,
            Pos.AnchorEnd(1),
            () => RunTransition(_logic.ShowActionMenu));
        _body.Add(table, info);
        table.SetFocus();
    }

    private void RenderFields(AssetField fieldTree)
    {
        var output = new TextViewer(FormatFieldTree(fieldTree))
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2)
        };
        AddButton(
            _strings.InspectPage_ReturnAction,
            Pos.AnchorEnd(1),
            () => RunTransition(_logic.ShowActionMenu));
        _body.Add(output);
        output.SetFocus();
    }

    private void RenderWorking()
    {
        _body.Add(new WorkingIndicator(_strings.InspectPage_Analyzing)
        {
            X = 0,
            Y = 0
        });
    }

    private void RenderError(string message)
    {
        var error = new StyledLabel(message, TextRole.Error)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        };
        ActionButton back = AddButton(
            _strings.InspectPage_ReturnAction,
            2,
            () => RunTransition(_logic.ShowActionMenu));
        _body.Add(error);
        back.SetFocus();
    }

    private void RunTransition(Action transition)
    {
        transition();
        RenderState();
    }

    private async Task RunLogicAsync(Func<Task> startOperation)
    {
        Task operation = startOperation();
        RenderState();
        RenderRequested?.Invoke(this, EventArgs.Empty);

        await operation;

        if (_isDisposed)
        {
            return;
        }

        RenderState();
    }

    private void SetActionMenuHeader()
    {
        SetHeader(
            _strings.MainMenu_InspectAssets_Title,
            _strings.InspectPage_Description);
    }

    private void SetListPathHeader()
    {
        SetHeader(
            _strings.InspectPage_ListAssetsTitle,
            _strings.InspectPage_ListAssetsDescription);
    }

    private void SetListLimitHeader()
    {
        SetHeader(
            _strings.InspectPage_RowsToPrintTitle,
            _strings.InspectPage_ListAssetsDescription);
    }

    private void SetFieldsHeader()
    {
        SetHeader(
            _strings.InspectPage_ShowFieldsTitle,
            _strings.InspectPage_ShowFieldsDescription);
    }

    private void SetOperationHeader(InspectAssetsOperation operation)
    {
        if (operation == InspectAssetsOperation.ListAssets)
        {
            SetListLimitHeader();
        }
        else
        {
            SetFieldsHeader();
        }
    }

    private ActionButton AddButton(
        string text,
        Pos y,
        Action action,
        ActionKind kind = ActionKind.Default)
    {
        var button = new ActionButton(text, kind)
        {
            X = 0,
            Y = y
        };
        button.Accepted += (_, _) => action();
        _body.Add(button);
        return button;
    }

    private ActionButton AddButton(
        string text,
        Pos y,
        Func<Task> action,
        ActionKind kind = ActionKind.Default)
    {
        var button = new ActionButton(text, kind)
        {
            X = 0,
            Y = y
        };
        button.Accepted += async (_, _) => await action();
        _body.Add(button);
        return button;
    }

    private static string FormatFieldTree(AssetField root)
    {
        var text = new StringBuilder();
        AppendField(text, root, 0);
        return text.ToString().TrimEnd();
    }

    private static void AppendField(StringBuilder text, AssetField field, int depth)
    {
        text.Append(' ', depth * 2).Append(field.Name).Append(" (").Append(field.TypeName).Append(')');
        if (field.Value is not null)
        {
            text.Append(": ").Append(field.Value.ToInvariantString());
        }

        text.AppendLine();
        foreach (AssetField child in field.Children)
        {
            AppendField(text, child, depth + 1);
        }
    }

    private sealed class AssetsTableSource : ITableSource
    {
        private readonly LocalizedStrings _strings;
        private readonly IReadOnlyList<InspectAssetSummary> _assets;

        public AssetsTableSource(
            LocalizedStrings strings,
            IReadOnlyList<InspectAssetSummary> assets)
        {
            _strings = strings;
            _assets = assets;
        }

        public string[] ColumnNames =>
        [
            _strings.InspectPage_PathIdColumn,
            _strings.InspectPage_TypeNameColumn,
            _strings.InspectPage_NameColumn
        ];

        public int Columns => 3;
        public int Rows => _assets.Count;

        public object this[int row, int col] => col switch
        {
            0 => _assets[row].PathId.ToString(CultureInfo.InvariantCulture),
            1 => _assets[row].TypeName,
            2 => _assets[row].Name ?? string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(col))
        };
    }
}
