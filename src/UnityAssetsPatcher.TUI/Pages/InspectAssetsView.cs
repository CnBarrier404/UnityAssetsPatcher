using System.Globalization;
using System.Text;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Domain.Assets;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class InspectAssetsView : View, ITerminalRenderRequester
{
    public event EventHandler? RenderRequested;

    private readonly IWorkflowService _workflowService;
    private readonly TerminalTaskRunner _taskRunner;
    private readonly StyledLabel _heading;
    private readonly StyledLabel _description;
    private readonly View _body;
    private bool _isWorking;

    private const int DefaultLimit = 100;

    public InspectAssetsView(
        IWorkflowService workflowService,
        TerminalTaskRunner taskRunner,
        Action returnToMainMenu)
    {
        _workflowService = workflowService;
        _taskRunner = taskRunner;

        KeyDown += (_, key) =>
        {
            if (key != Key.Esc)
            {
                return;
            }

            key.Handled = true;

            if (_isWorking)
            {
                return;
            }

            returnToMainMenu.Invoke();
        };

        _heading = new StyledLabel(role: TextRole.Title)
        {
            X = 0, Y = 0
        };

        _description = new StyledLabel(role: TextRole.Muted)
        {
            X = 0, Y = 1,
            Width = Dim.Fill()
        };

        _body = new View
        {
            X = 0, Y = 3,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true
        };

        Add(_heading, _description, _body);

        ShowActionMenu();
    }

    private void ShowActionMenu()
    {
        SetPage(LocalizedStrings.MainMenu_InspectAssets_Title, LocalizedStrings.InspectPage_Description);

        _body.RemoveAll();

        Button list = AddChoice(
            LocalizedStrings.InspectPage_ListAssetsTitle,
            LocalizedStrings.InspectPage_ListAssetsDescription,
            0);

        list.Accepted += (_, _) => ShowListPathInput();

        Button fields = AddChoice(
            LocalizedStrings.InspectPage_ShowFieldsTitle,
            LocalizedStrings.InspectPage_ShowFieldsDescription,
            2);

        fields.Accepted += (_, _) => ShowFieldsInput();

        list.SetFocus();
    }

    private ActionButton AddChoice(string text, string description, int row)
    {
        var choice = new ChoiceItem(text, description)
        {
            X = 0, Y = row
        };

        _body.Add(choice);

        return choice.Button;
    }

    private void ShowListPathInput()
    {
        SetPage(LocalizedStrings.InspectPage_ListAssetsTitle, LocalizedStrings.InspectPage_ListAssetsDescription);
        ShowPathInput(ShowLimitChoices);
    }

    private void ShowPathInput(Action<string> accepted)
    {
        _body.RemoveAll();
        string prompt = $"{LocalizedStrings.InspectPage_AssetsFilePathPrompt}: ";

        var label = new StyledLabel(prompt, TextRole.Label)
        {
            X = 0, Y = 0
        };

        var input = new InputField
        {
            X = prompt.GetColumns(),
            Y = 0,
            Width = Dim.Fill()
        };

        var error = new StyledLabel(role: TextRole.Error)
        {
            X = 0, Y = 2,
            Width = Dim.Fill(),
            Visible = false
        };

        input.Accepted += (_, _) =>
        {
            string path = TerminalPathNormalizer.Normalize(input.Text);

            if (!File.Exists(path))
            {
                input.Text = string.Empty;
                error.Text = string.Format(LocalizedStrings.Prompt_FileNotFoundFormat, path);
                error.Visible = true;
                input.SetFocus();

                return;
            }

            input.Text = path;
            accepted(Path.GetFullPath(path));
        };

        Button back = CreateActionButton(LocalizedStrings.InspectPage_BackAction, 0, 4);
        back.Accepted += (_, _) => ShowActionMenu();
        _body.Add(label, input, error, back);
        input.SetFocus();
    }

    private void ShowLimitChoices(string assetsFilePath)
    {
        SetPage(LocalizedStrings.InspectPage_RowsToPrintTitle, LocalizedStrings.InspectPage_ListAssetsDescription);
        _body.RemoveAll();
        Button first = CreateActionButton(LocalizedStrings.InspectPage_First100Choice, 0, 0);
        first.Accepted += (_, _) => InspectList(assetsFilePath, DefaultLimit);
        Button all = CreateActionButton(LocalizedStrings.InspectPage_AllRowsChoice, 0, 2);
        all.Accepted += (_, _) => InspectList(assetsFilePath, null);
        Button custom = CreateActionButton(LocalizedStrings.InspectPage_CustomLimitChoice, 0, 4);
        custom.Accepted += (_, _) => ShowCustomLimitInput(assetsFilePath);
        Button back = CreateActionButton(LocalizedStrings.InspectPage_BackAction, 0, 6);
        back.Accepted += (_, _) => ShowListPathInput();
        _body.Add(first, all, custom, back);
        first.SetFocus();
    }

    private void ShowCustomLimitInput(string assetsFilePath)
    {
        _body.RemoveAll();
        string prompt = $"{LocalizedStrings.InspectPage_MaximumRowsPrompt}: ";
        var label = new StyledLabel(prompt, TextRole.Label) { X = 0, Y = 0 };
        var input = new InputField { X = prompt.GetColumns(), Y = 0, Width = 12 };
        var error = new StyledLabel(role: TextRole.Error)
            { X = 0, Y = 2, Width = Dim.Fill(), Visible = false };
        input.Accepted += (_, _) =>
        {
            if (!int.TryParse(input.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit) ||
                limit <= 0)
            {
                input.Text = string.Empty;
                error.Text = string.Format(LocalizedStrings.Prompt_InvalidPositiveIntegerFormat,
                    LocalizedStrings.InspectPage_MaximumRowsPrompt);
                error.Visible = true;
                input.SetFocus();
                return;
            }

            InspectList(assetsFilePath, limit);
        };
        Button back = CreateActionButton(LocalizedStrings.InspectPage_BackAction, 0, 4);
        back.Accepted += (_, _) => ShowLimitChoices(assetsFilePath);
        _body.Add(label, input, error, back);
        input.SetFocus();
    }

    private void InspectList(string path, int? limit)
    {
        if (_isWorking)
        {
            return;
        }

        bool started = _taskRunner.TryRun(
            () => _workflowService.InspectList(new InspectListRequest(path, limit)),
            result =>
            {
                _isWorking = false;
                ShowAssets(result);
            },
            exception =>
            {
                _isWorking = false;
                ShowError(exception.Message);
            });

        if (!started)
        {
            return;
        }

        _isWorking = true;
        ShowWorking();
    }

    private void ShowAssets(InspectListResult result)
    {
        _body.RemoveAll();
        var table = new DataTableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
            Table = new AssetsTableSource(result.Assets),
        };
        string infoText = result.Assets.Count < result.TotalCount
            ? string.Format(CultureInfo.CurrentUICulture, LocalizedStrings.InspectPage_ShowingAssetsFormat,
                result.Assets.Count, result.TotalCount)
            : string.Empty;
        var info = new StyledLabel(infoText, TextRole.Muted)
            { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill() };
        Button back = CreateActionButton(LocalizedStrings.InspectPage_ReturnAction, 0, Pos.AnchorEnd(1));
        back.Accepted += (_, _) => ShowActionMenu();
        _body.Add(table, info, back);
        table.SetFocus();
    }

    private void ShowFieldsInput()
    {
        SetPage(LocalizedStrings.InspectPage_ShowFieldsTitle, LocalizedStrings.InspectPage_ShowFieldsDescription);
        _body.RemoveAll();
        string pathPrompt = $"{LocalizedStrings.InspectPage_AssetsFilePathPrompt}: ";
        var pathLabel = new StyledLabel(pathPrompt, TextRole.Label) { X = 0, Y = 0 };
        var pathInput = new InputField
            { X = pathPrompt.GetColumns(), Y = 0, Width = Dim.Fill() };
        string idPrompt = $"{LocalizedStrings.InspectPage_PathIdPrompt}: ";
        var idLabel = new StyledLabel(idPrompt, TextRole.Label) { X = 0, Y = 2 };
        var idInput = new InputField { X = idPrompt.GetColumns(), Y = 2, Width = 20 };
        var error = new StyledLabel(role: TextRole.Error)
            { X = 0, Y = 4, Width = Dim.Fill(), Visible = false };

        void Submit()
        {
            string path = TerminalPathNormalizer.Normalize(pathInput.Text);
            if (!File.Exists(path))
            {
                pathInput.Text = string.Empty;
                error.Text = string.Format(LocalizedStrings.Prompt_FileNotFoundFormat, path);
                error.Visible = true;
                pathInput.SetFocus();
                return;
            }

            if (!long.TryParse(idInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long pathId))
            {
                idInput.Text = string.Empty;
                error.Text = string.Format(LocalizedStrings.Prompt_InvalidIntegerFormat,
                    LocalizedStrings.InspectPage_PathIdPrompt);
                error.Visible = true;
                idInput.SetFocus();
                return;
            }

            InspectFields(Path.GetFullPath(path), pathId);
        }

        pathInput.Accepted += (_, _) => idInput.SetFocus();
        idInput.Accepted += (_, _) => Submit();
        Button inspect = CreatePrimaryActionButton(LocalizedStrings.InspectPage_ShowFieldsTitle, 0, 6);
        inspect.Accepted += (_, _) => Submit();
        Button back = CreateActionButton(LocalizedStrings.InspectPage_BackAction, 0, 8);
        back.Accepted += (_, _) => ShowActionMenu();
        _body.Add(pathLabel, pathInput, idLabel, idInput, error, inspect, back);
        pathInput.SetFocus();
    }

    private void InspectFields(string path, long pathId)
    {
        if (_isWorking)
        {
            return;
        }

        bool started = _taskRunner.TryRun(
            () => _workflowService.InspectFields(new InspectFieldsRequest(path, pathId)),
            result =>
            {
                _isWorking = false;
                ShowFields(result);
            },
            exception =>
            {
                _isWorking = false;
                ShowError(exception.Message);
            });

        if (!started)
        {
            return;
        }

        _isWorking = true;
        ShowWorking();
    }

    private void ShowFields(AssetField fieldTree)
    {
        _body.RemoveAll();
        var output = new TextViewer(FormatFieldTree(fieldTree))
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(2),
        };
        Button back = CreateActionButton(LocalizedStrings.InspectPage_ReturnAction, 0, Pos.AnchorEnd(1));
        back.Accepted += (_, _) => ShowActionMenu();
        _body.Add(output, back);
        output.SetFocus();
    }

    private void ShowWorking()
    {
        _body.RemoveAll();
        var status = new StyledLabel(LocalizedStrings.InspectPage_Analyzing, TextRole.Preview)
            { X = 0, Y = 0, Width = Dim.Fill() };
        _body.Add(status);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowError(string message)
    {
        _body.RemoveAll();
        var error = new StyledLabel(message, TextRole.Error)
            { X = 0, Y = 0, Width = Dim.Fill() };
        Button back = CreateActionButton(LocalizedStrings.InspectPage_ReturnAction, 0, 2);
        back.Accepted += (_, _) => ShowActionMenu();
        _body.Add(error, back);
        back.SetFocus();
    }

    private void SetPage(string title, string description)
    {
        _heading.Text = title;
        _description.Text = description;
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
        if (field.Value is not null) text.Append(": ").Append(field.Value.ToInvariantString());
        text.AppendLine();
        foreach (AssetField child in field.Children) AppendField(text, child, depth + 1);
    }

    private static ActionButton CreateActionButton(string text, Pos x, Pos y)
    {
        return new ActionButton(text) { X = x, Y = y };
    }

    private static ActionButton CreatePrimaryActionButton(string text, Pos x, Pos y)
    {
        return new ActionButton(text, ActionKind.Primary)
        {
            X = x, Y = y
        };
    }

    private sealed class AssetsTableSource : ITableSource
    {
        private readonly IReadOnlyList<InspectAssetSummary> _assets;
        public AssetsTableSource(IReadOnlyList<InspectAssetSummary> assets) => _assets = assets;

        public string[] ColumnNames =>
        [
            LocalizedStrings.InspectPage_PathIdColumn, LocalizedStrings.InspectPage_TypeNameColumn,
            LocalizedStrings.InspectPage_NameColumn
        ];

        public int Columns => 3;
        public int Rows => _assets.Count;

        public object this[int row, int col] => col switch
        {
            0 => _assets[row].PathId.ToString(CultureInfo.InvariantCulture),
            1 => _assets[row].TypeName,
            2 => _assets[row].Name ?? string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(col)),
        };
    }
}
