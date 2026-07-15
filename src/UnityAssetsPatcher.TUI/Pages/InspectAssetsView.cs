using System.Globalization;
using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;
using UnityAssetsPatcher.TUI.Shell;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace UnityAssetsPatcher.TUI.Pages;

public sealed class InspectAssetsView : View, ITerminalContentView, ITerminalRenderRequester
{
    private const int DefaultLimit = 100;

    public event EventHandler? RenderRequested;

    public string ShortcutHint => LocalizedStrings.Layout_ShortcutHint;

    private readonly IWorkflowService _workflowService;
    private readonly Label _heading;
    private readonly Label _description;
    private readonly View _body;
    private bool _isWorking;

    public InspectAssetsView(IWorkflowService workflowService, Action returnToMainMenu)
    {
        _workflowService = workflowService;

        Action returnToMainMenu1 = returnToMainMenu;

        KeyDown += (_, key) =>
        {
            if (key != Key.Esc || _isWorking)
            {
                return;
            }

            key.Handled = true;
            returnToMainMenu1();
        };

        _heading = new Label { X = 0, Y = 0 };
        _heading.SetScheme(TerminalGUITheme.Title);
        _description = new Label { X = 0, Y = 1, Width = Dim.Fill() };
        _description.SetScheme(TerminalGUITheme.Muted);
        _body = new View { X = 0, Y = 3, Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
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

    private Button AddChoice(string text, string description, int row)
    {
        string normalText = $"  {text}";
        string focusedText = $"> {text}";
        var button = new Button
        {
            Text = normalText, X = 0, Y = row, Width = 30, NoDecorations = true, NoPadding = true,
            ShadowStyle = ShadowStyles.None, TextAlignment = Alignment.Start,
        };
        button.SetScheme(CreateChoiceScheme());
        var details = new Label { Text = description, X = 36, Y = row, Width = Dim.Fill() };
        details.SetScheme(TerminalGUITheme.Muted);
        button.HasFocusChanged += (_, _) =>
        {
            button.Text = button.HasFocus ? focusedText : normalText;
            details.SetScheme(button.HasFocus ? TerminalGUITheme.Selected : TerminalGUITheme.Muted);
        };
        _body.Add(button, details);
        return button;
    }

    private void ShowListPathInput()
    {
        SetPage(LocalizedStrings.InspectPage_ListAssetsTitle, LocalizedStrings.InspectPage_ListAssetsDescription);
        ShowPathInput(path => ShowLimitChoices(path));
    }

    private void ShowPathInput(Action<string> accepted)
    {
        _body.RemoveAll();
        string prompt = $"{LocalizedStrings.InspectPage_AssetsFilePathPrompt}: ";
        var label = new Label { Text = prompt, X = 0, Y = 0 };
        label.SetScheme(TerminalGUITheme.Label);
        var input = new TextField { X = GetDisplayWidth(prompt), Y = 0, Width = Dim.Fill() };
        input.SetScheme(CreateInputScheme());
        var error = new Label { X = 0, Y = 2, Width = Dim.Fill(), Visible = false };
        error.SetScheme(TerminalGUITheme.Error);
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
        var label = new Label { Text = prompt, X = 0, Y = 0 };
        label.SetScheme(TerminalGUITheme.Label);
        var input = new TextField { X = GetDisplayWidth(prompt), Y = 0, Width = 12 };
        input.SetScheme(CreateInputScheme());
        var error = new Label { X = 0, Y = 2, Width = Dim.Fill(), Visible = false };
        error.SetScheme(TerminalGUITheme.Error);
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
        if (_isWorking) return;
        _isWorking = true;
        ShowWorking();
        try
        {
            InspectListResult result = _workflowService.InspectList(new InspectListRequest(path, limit));
            ShowAssets(result);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _isWorking = false;
        }
    }

    private void ShowAssets(InspectListResult result)
    {
        _body.RemoveAll();
        var table = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
            BorderStyle = LineStyle.None,
            Table = new AssetsTableSource(result.Assets),
            Style = new TableStyle
            {
                ShowHeaders = true,
                AlwaysShowHeaders = true,
                ShowHorizontalBottomLine = false,
                ShowHorizontalHeaderOverline = false,
                ShowHorizontalHeaderUnderline = true,
                ShowVerticalCellLines = false,
                ShowVerticalCellLineForFirstColumn = false,
                ShowVerticalCellLineForLastColumn = false,
                ShowVerticalHeaderLines = false,
                ExpandLastColumn = true,
            },
        };
        table.SetScheme(CreateTableScheme());
        string infoText = result.Assets.Count < result.TotalCount
            ? string.Format(CultureInfo.CurrentUICulture, LocalizedStrings.InspectPage_ShowingAssetsFormat,
                result.Assets.Count, result.TotalCount)
            : string.Empty;
        var info = new Label { Text = infoText, X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill() };
        info.SetScheme(TerminalGUITheme.Muted);
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
        var pathLabel = new Label { Text = pathPrompt, X = 0, Y = 0 };
        pathLabel.SetScheme(TerminalGUITheme.Label);
        var pathInput = new TextField { X = GetDisplayWidth(pathPrompt), Y = 0, Width = Dim.Fill() };
        pathInput.SetScheme(CreateInputScheme());
        string idPrompt = $"{LocalizedStrings.InspectPage_PathIdPrompt}: ";
        var idLabel = new Label { Text = idPrompt, X = 0, Y = 2 };
        idLabel.SetScheme(TerminalGUITheme.Label);
        var idInput = new TextField { X = GetDisplayWidth(idPrompt), Y = 2, Width = 20 };
        idInput.SetScheme(CreateInputScheme());
        var error = new Label { X = 0, Y = 4, Width = Dim.Fill(), Visible = false };
        error.SetScheme(TerminalGUITheme.Error);

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
        if (_isWorking) return;
        _isWorking = true;
        ShowWorking();
        try
        {
            AssetsFieldInfo result = _workflowService.InspectFields(new InspectFieldsRequest(path, pathId));
            ShowFields(result);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _isWorking = false;
        }
    }

    private void ShowFields(AssetsFieldInfo fieldTree)
    {
        _body.RemoveAll();
#pragma warning disable CS0618 // Terminal.Gui has no bundled read-only, scrollable replacement yet.
        var output = new TextView
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(2), ReadOnly = true,
            Text = FormatFieldTree(fieldTree),
        };
#pragma warning restore CS0618
        output.SetScheme(TerminalGUITheme.Base);
        Button back = CreateActionButton(LocalizedStrings.InspectPage_ReturnAction, 0, Pos.AnchorEnd(1));
        back.Accepted += (_, _) => ShowActionMenu();
        _body.Add(output, back);
        output.SetFocus();
    }

    private void ShowWorking()
    {
        _body.RemoveAll();
        var status = new Label { Text = LocalizedStrings.InspectPage_Analyzing, X = 0, Y = 0, Width = Dim.Fill() };
        status.SetScheme(TerminalGUITheme.Preview);
        _body.Add(status);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowError(string message)
    {
        _body.RemoveAll();
        var error = new Label { Text = message, X = 0, Y = 0, Width = Dim.Fill() };
        error.SetScheme(TerminalGUITheme.Error);
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

    private static string FormatFieldTree(AssetsFieldInfo root)
    {
        var text = new StringBuilder();
        AppendField(text, root, 0);
        return text.ToString().TrimEnd();
    }

    private static void AppendField(StringBuilder text, AssetsFieldInfo field, int depth)
    {
        text.Append(' ', depth * 2).Append(field.Name).Append(" (").Append(field.TypeName).Append(')');
        if (field.Value is not null) text.Append(": ").Append(field.Value.ToInvariantString());
        text.AppendLine();
        foreach (AssetsFieldInfo child in field.Children) AppendField(text, child, depth + 1);
    }

    private static Button CreateActionButton(string text, Pos x, Pos y)
    {
        string normal = $"  {text}";
        string focused = $"> {text}";
        var button = new Button
        {
            Text = normal, X = x, Y = y, NoDecorations = true, NoPadding = true,
            ShadowStyle = ShadowStyles.None, TextAlignment = Alignment.Start,
        };
        button.SetScheme(CreateChoiceScheme());
        button.HasFocusChanged += (_, _) => button.Text = button.HasFocus ? focused : normal;
        return button;
    }

    private static Button CreatePrimaryActionButton(string text, Pos x, Pos y)
    {
        Button button = CreateActionButton(text, x, y);
        Attribute normal = TerminalGUITheme.Label.Normal;
        Attribute selected = TerminalGUITheme.Selected.Normal;
        button.SetScheme(CreateScheme(normal, selected));
        return button;
    }

    private static Scheme CreateChoiceScheme() =>
        CreateScheme(TerminalGUITheme.Base.Normal, TerminalGUITheme.Selected.Normal);

    private static Scheme CreateInputScheme() => CreateChoiceScheme();

    private static Scheme CreateTableScheme() =>
        CreateScheme(TerminalGUITheme.Base.Normal, TerminalGUITheme.Selected.Normal);

    private static Scheme CreateScheme(Attribute normal, Attribute selected) => new()
    {
        Normal = normal, Focus = selected, HotNormal = normal, HotFocus = selected, Active = selected,
        Editable = normal, ReadOnly = normal, Disabled = normal,
    };

    private static int GetDisplayWidth(string value)
    {
        return value.Sum(character => character is >= '\u1100' and <= '\u115f' or >= '\u2e80' and <= '\ua4cf'
            or >= '\uac00' and <= '\ud7a3' or >= '\uf900' and <= '\ufaff' or >= '\ufe10' and <= '\ufe19'
            or >= '\ufe30' and <= '\ufe6f' or >= '\uff00' and <= '\uff60' or >= '\uffe0' and <= '\uffe6'
            ? 2
            : 1);
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
